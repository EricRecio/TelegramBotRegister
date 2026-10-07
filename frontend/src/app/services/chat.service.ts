import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';

// Interfaces del Dominio del Frontend
export interface Contact {
  id: number;
  username: string;
  firstName: string;
  unreadCount: number;
  lastMessagePreview: string;
  lastMessageAt: string;
}

export interface Message {
  id: string;
  text: string;
  sentAt: string;
  isFromOperator: boolean;
  isRead: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:8080/api/chat';
  private hubConnection!: signalR.HubConnection;

  // 🚥 SIGNALS: El estado reactivo de nuestra bandeja web
  public contacts = signal<Contact[]>([]);
  public activeChatId = signal<number | null>(null);
  public activeChatMessages = signal<Message[]>([]);

  constructor() {
    this.initializeSignalR();
    this.loadContacts();
  }

  // 1. Obtener la lista de contactos inicial por HTTP
  public loadContacts(): void {
    this.http.get<Contact[]>(`${this.apiUrl}/contacts`).subscribe({
      next: (data) => this.contacts.set(data),
      error: (err) => console.error('Error al cargar contactos:', err)
    });
  }

  // 2. Seleccionar un contacto, traer su historial y limpiar no leídos localmente
  public selectContact(chatId: number): void {
    this.activeChatId.set(chatId);
    
    this.http.get<{ contact: Contact, messages: Message[] }>(`${this.apiUrl}/contacts/${chatId}/messages`)
      .subscribe({
        next: (res) => {
          this.activeChatMessages.set(res.messages);
          
          // Actualizar el contador de no leídos a 0 en la lista de contactos local
          this.contacts.update(list => 
            list.map(c => c.id === chatId ? { ...c, unreadCount: 0 } : c)
          );
        },
        error: (err) => console.error('Error al cargar historial:', err)
      });
  }

  // 3. Responder al contacto desde la bandeja web
  public sendReply(text: string): void {
    const chatId = this.activeChatId();
    if (!chatId || !text.trim()) return;

    this.http.post<Message>(`${this.apiUrl}/contacts/${chatId}/reply`, { text })
      .subscribe({
        next: (newMessage) => {
          // Agregar el mensaje enviado por el operador a la conversación actual
          this.activeChatMessages.update(msgs => [...msgs, newMessage]);

          // Actualizar la vista previa del contacto en la barra lateral
          this.contacts.update(list =>
            list.map(c => c.id === chatId ? { ...c, lastMessagePreview: text, lastMessageAt: newMessage.sentAt } : c)
              .sort((a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime())
          );
        },
        error: (err) => console.error('Error al enviar respuesta:', err)
      });
  }

  // 4. Configurar e iniciar la conexión en tiempo real con SignalR
  private initializeSignalR(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:8080/chathub', {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.start()
      .then(() => console.log('⚡ Conexión exitosa con SignalR ChatHub'))
      .catch(err => console.error('Error al iniciar SignalR:', err));

    // Escuchar el evento "ReceiveMessage" emitido por el Consumidor del Backend
    this.hubConnection.on('ReceiveMessage', (data: any) => {
      
      // Caso A: El mensaje entrante pertenece al chat que el operador tiene abierto actualmente
      if (this.activeChatId() === data.chatId) {
        const incomingMsg: Message = {
          id: data.message.id,
          text: data.message.text,
          sentAt: data.message.sentAt,
          isFromOperator: data.message.isFromOperator,
          isRead: true
        };
        this.activeChatMessages.update(msgs => [...msgs, incomingMsg]);
        
        // Avisar al backend que lo marque como leído de inmediato
        this.http.get(`${this.apiUrl}/contacts/${data.chatId}/messages`).subscribe();
      }

      // Caso B: Actualizar de forma reactiva la lista lateral de contactos
      this.contacts.update(list => {
        const contactIndex = list.findIndex(c => c.id === data.chatId);
        const currentUnread = contactIndex !== -1 ? list[contactIndex].unreadCount : 0;
        const nextUnread = this.activeChatId() === data.chatId ? 0 : currentUnread + 1;

        const updatedContact: Contact = {
          id: data.chatId,
          username: data.username,
          firstName: data.firstName,
          unreadCount: nextUnread,
          lastMessagePreview: data.message.text,
          lastMessageAt: data.message.sentAt
        };

        if (contactIndex !== -1) {
          const filtered = list.filter(c => c.id !== data.chatId);
          return [updatedContact, ...filtered];
        } else {
          return [updatedContact, ...list];
        }
      });
    });
  }
}
