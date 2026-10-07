# Informe de Trabajo con Inteligencia Artificial

## 1. Herramientas Utilizadas y Roles
Para la concepción, estructuración y auditoría de la solución se utilizó un modelo LLM avanzado operado bajo tres roles técnicos específicos:
* **Arquitecto Full-Stack:** Encargado de estructurar la solución en C# con Clean Architecture y la reactividad en Angular 19 mediante Signals.
* **Especialista DevOps:** Diseñador del entorno de orquestación integrado en Docker Compose y la topología de mensajería en RabbitMQ.
* **Auditor de Seguridad e Infraestructura (SecOps):** Enfocado en mitigar riesgos informáticos en dependencias y la validación de sintaxis en contenedores.

## 2. Prompts Representativos Utilizados
* **Estructura Asíncrona (Backend):** *"Diseña un BackgroundService en .NET 10 que funcione como consumidor persistente de RabbitMQ para la cola 'telegram.received_messages' utilizando la sintaxis asíncrona oficial de RabbitMQ.Client (v7+). Debe persistir los datos en PostgreSQL empleando Scopes para el DbContext e integrar SignalR para empujar los cambios en tiempo real."*
* **Reactividad Eficiente (Frontend):** *"Genera un servicio Standalone en Angular 19 que implemente el cliente de SignalR. Gestiona el estado de la bandeja de entrada (contactos y chat activo) empleando estrictamente Angular Signals para asegurar un renderizado eficiente y libre de sobrecarga en el navegador."*

## 3. Revisión, Corrección y Validación Humana
El código sugerido por la IA pasó por una estricta fase de refactorización y control de calidad manual por parte del desarrollador:
1. **Blindaje contra Denegación de Servicio (DoS):** Se vetó el uso heredado de `Newtonsoft.Json` (v11.0.2) debido a la vulnerabilidad conocida de agotamiento de pila (GHSA-5crp-9r3c-p9vr). Toda la serialización se migró nativamente al motor **`System.Text.Json`** de alto rendimiento en .NET 10.
2. **Configuración de Contextos de Compilación:** Se corrigieron manualmente los Dockerfiles de la solución eliminando las extensiones de texto `.txt` que generaba el explorador de Windows e independizando los contextos (`./PruebaTecnicaEncodelabs` y `./frontend`) en Docker Compose para resolver colisiones de dependencias entre las capas.
3. **Resolución de Dependencias en TypeScript:** Se subsanaron errores de compilación en el frontend causados por la omisión de la función nativa `inject` dentro de los módulos Standalone de Angular 19.
