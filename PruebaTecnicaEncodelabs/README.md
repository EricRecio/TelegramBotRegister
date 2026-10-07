# 📥 Telegram Chat Inbox - Bandeja Unificada en Tiempo Real

Este proyecto implementa una bandeja de entrada web unificada que permite a los operadores recibir, visualizar y responder en tiempo real los mensajes de texto enviados por los clientes a un Bot de Telegram.

La solución está construida bajo los principios de **Clean Architecture** empleando una arquitectura dirigida por eventos (EDA) de alta disponibilidad.

---

## 🛠️ 1. Requisitos Previos

Antes de levantar la solución, asegúrate de contar con las siguientes herramientas instaladas en tu equipo:

1. **Docker y Docker Desktop** (Con soporte para Docker Compose).
2. **Node.js** (Versión 20 LTS o superior, solo requerido si deseas correr el frontend de forma local sin contenedores).
3. **Un Bot de Telegram:** Creado a través de `@BotFather` para obtener tu `HTTP API Token`.
4. **Una cuenta de ngrok:** Requerida para obtener un `Authtoken` y configurar un dominio estático gratuito.

---

## 🔐 2. Configuración del Entorno (.env)

En la raíz del proyecto encontrarás el archivo `.env.example`. Copia este archivo y renombralo como `.env` de la siguiente manera:

```bash
cp .env.example .env
```

Abre el archivo `.env` y completa los siguientes campos con tus credenciales reales:

* `TELEGRAM_BOT_TOKEN`: El token secreto de tu bot provisto por @BotFather.
* `NGROK_AUTHTOKEN`: Tu token de autenticación del panel de ngrok.
* `NGROK_CUSTOM_DOMAIN`: Tu dominio estático gratuito asignado por ngrok (ejemplo: `mi-proyecto.ngrok-free.app`). **Nota:** Las cuentas gratuitas modernas de ngrok exigen el uso de este dominio estático para evitar que la URL cambie en cada reinicio.
* `DB_USER` y `DB_PASSWORD`: Credenciales a elección para la base de datos de PostgreSQL.
* `RABBITMQ_USER` y `RABBITMQ_PASSWORD`: Credenciales a elección para el panel web de RabbitMQ.

---

## 🚀 3. Cómo Levantar la Solución

Gracias a la orquestación centralizada, puedes inicializar todo el ecosistema (PostgreSQL, RabbitMQ, ngrok, Backend .NET 10 y Frontend Angular 19) con un único comando ejecutado en la raíz del proyecto:

```bash
docker compose up --build
```

### ⚙️ Automatizaciones de Arranque Incluidas:
* **Migraciones Automáticas:** El backend en .NET 10 detectará el contenedor de PostgreSQL, creará la base de datos y aplicará las tablas relacionales de forma autónoma.
* **Registro de Webhook Automatizado:** El backend consumirá la API interna de ngrok de forma segura, extraerá la URL pública activa y registrará el Webhook directamente en los servidores de Telegram sin intervención manual del usuario.

---

## 🧪 4. Cómo Probar la Solución de Punta a Punta

Una vez que todos los contenedores muestren un estado activo en tu terminal:

1. **Abrir la Bandeja Web:** Abre tu navegador web e ingresa a `http://localhost:4200`. Verás la interfaz con el diseño oscuro inspirado en *Encodelabs* mostrando un estado inicial vacío.
2. **Simular Mensaje de Cliente:** Abre la aplicación de Telegram (en tu celular o PC), busca tu bot y envíale un mensaje de texto de prueba (ej: *"Hola, necesito soporte"*).
3. **Recepción en Tiempo Real:** Observa la bandeja web en tu navegador. Sin necesidad de recargar la página, verás aparecer el nuevo contacto en la barra lateral con su nombre/username, una burbuja indicando `1` mensaje no leído y la vista previa del texto gracias a la conexión por WebSockets de SignalR.
4. **Lectura e Historial:** Haz clic sobre el contacto. La bandeja web traerá el historial de la conversación desde PostgreSQL y limpiará inmediatamente el contador de no leídos a `0` en la barra lateral.
5. **Responder al Cliente:** Escribe un mensaje en el cuadro de texto central (ej: *"Hola, ¿en qué te puedo ayudar hoy?"*) y haz clic en el botón **Enviar**. El mensaje se guardará en tu base de datos y llegará de forma instantánea al chat de Telegram del usuario remitente enviado por el bot.

---

## 📌 5. Supuestos Adoptados

Ante los requerimientos no definidos explícitamente en las consignas, se tomaron las siguientes decisiones de diseño:

1. **Dominios Estáticos de ngrok:** Debido a las políticas actuales de ngrok, las URLs públicas aleatorias suelen bloquearse o exigir pantallas de advertencia intermedias si no se validan correctamente. Se optó por incluir la variable `NGROK_CUSTOM_DOMAIN` para garantizar que el Webhook de Telegram apunte a un endpoint estático, transparente y seguro.
2. **Nombres de Usuario de Telegram:** Telegram no obliga a todos los usuarios a configurar un `@username` público. Como supuesto de diseño, si un cliente interactúa con el bot y no posee un username configurado, el sistema inicializará su registro visual utilizando su `First Name` (Nombre) seguido de su ID de chat para evitar campos vacíos o nulos en la barra lateral.
3. **Confirmación de Entrega Asíncrona:** Cuando el operador envía una respuesta, la API primero despacha el mensaje hacia los servidores de Telegram por HTTP. Si la API de Telegram responde con éxito, recién ahí el registro se persiste en PostgreSQL de forma transaccional, garantizando que el historial web refleje fielmente lo que el usuario final realmente recibió en su pantalla de Telegram.
