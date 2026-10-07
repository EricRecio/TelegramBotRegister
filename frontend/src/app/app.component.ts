import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatService } from './services/chat.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  // Inyección del servicio de datos en tiempo real [3.1, 3.2]
  private chatService = inject(ChatService);

  // Exponer los Signals del servicio para que el HTML se actualice automáticamente [3.1]
  public contacts = this.chatService.contacts;
  public activeChatId = this.chatService.activeChatId;
  public activeChatMessages = this.chatService.activeChatMessages;

  // Signal local para controlar el cuadro de texto del operador
  public replyText = signal<string>('');

  // Computar u obtener el contacto que está actualmente seleccionado
  public get selectedContact() {
    return this.contacts().find(c => c.id === this.activeChatId());
  }

  // Ejecuta la acción de seleccionar un contacto de la lista [3.1]
  public onSelectContact(chatId: number): void {
    this.chatService.selectContact(chatId);
    this.replyText.set(''); // Limpiar el cuadro de texto al cambiar de chat
  }

  // Ejecuta el envío de la respuesta hacia Telegram [3.1]
  public onSendReply(): void {
    const text = this.replyText().trim();
    if (!text) return;

    this.chatService.sendReply(text);
    this.replyText.set(''); // Vaciar el campo inmediatamente tras enviar
  }
}
