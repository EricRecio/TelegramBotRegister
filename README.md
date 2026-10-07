# ?? Telegram Chat Inbox - Bandeja Unificada en Tiempo Real

Este proyecto implementa una bandeja de entrada web unificada que permite a los operadores recibir, visualizar y responder en tiempo real los mensajes de texto enviados por los clientes a un Bot de Telegram.

La solución está construida bajo los principios de **Clean Architecture** empleando una arquitectura dirigida por eventos (EDA) de alta disponibilidad.

---

## ?? Supuestos de Diseño Adoptados

Ante los requerimientos no definidos explícitamente en la consigna original, se adoptaron las siguientes decisiones de diseño arquitectónico:

1. **Gestión de Identidades sin Username:** Dado que Telegram no obliga a los usuarios a configurar un `@username` público, el sistema mitiga la existencia de campos nulos en la interfaz inicializando el registro visual mediante el `First Name` (Nombre) del cliente, concatenado con su ID único de chat para asegurar la trazabilidad.
2. **Idempotencia en el Envío de Respuestas:** Al despachar un mensaje desde la bandeja web, el backend prioriza la consistencia del canal externo: primero invoca la API de Telegram de forma asíncrona; si y solo si el servidor de Telegram responde un `200 OK`, el mensaje se persiste en PostgreSQL, garantizando que el historial web sea un reflejo fiel de lo recibido por el usuario.

---

## ?? Respuestas a Preguntas Bonus (Sección 5.5)

### ¿Para qué usarías un sistema de caché en una bandeja conversacional?
Implementaría un sistema de caché distribuida (como Redis) para almacenar el estado consolidado del **listado de contactos activos** (con su respectiva vista previa del último mensaje y el contador de mensajes no leídos). Dado que esta es la vista de lectura más crítica y consultada constantemente por los operadores de soporte, servir estos agregados directamente desde la memoria RAM evita saturar el motor relacional con consultas repetitivas de ordenamiento (`ORDER BY`) y conteo (`COUNT`), optimizando drásticamente los tiempos de respuesta del servidor.

### ¿Cómo gestionarías el tráfico de archivos multimedia?
Diseñaría un flujo asíncrono y completamente desacoplado de la API principal. Al recibir el Webhook de Telegram con la metadata de un archivo (imagen, audio o documento), el controlador HTTP publicaría de inmediato un evento en una cola dedicada en RabbitMQ para liberar el hilo de conexión. Un Worker o microservicio secundario consumiría dicho evento, descargaría el binario desde los servidores de Telegram y lo almacenaría en un **proveedor de almacenamiento de objetos (como AWS S3 o MinIO)**. En la base de datos relacional transaccional únicamente se guardaría el identificador del recurso, el tipo MIME y la URL firmada de acceso, manteniendo la base de datos ligera y elástica.
