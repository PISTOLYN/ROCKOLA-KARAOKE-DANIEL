# ROCKOLA-KARAOKE-DANIEL

Aplicación de escritorio de Karaoke y Rockola (Jukebox) para Windows.

## Objetivo

Construir por etapas un sistema comercial con biblioteca multimedia, reproductor, cola de reproducción, créditos, modo VIP, administración y soporte para monedero electrónico Arduino por puerto serial.

## Plataforma objetivo

- Windows 7 SP1, Windows 10 y Windows 11 (la compatibilidad final depende de validar cada dependencia y el instalador en equipos reales).
- C# y Windows Forms.
- Base de datos SQLite local.
- Motor multimedia LibVLCSharp/VLC, sujeto a pruebas de compatibilidad por sistema operativo y arquitectura.

## Estado

**Etapa 0 – Repositorio inicializado.** La siguiente etapa establece la solución de escritorio, estructura por módulos y persistencia SQLite.

## Principios del proyecto

- Interfaz de cliente sencilla, visual y con controles grandes.
- Panel de administración separado.
- Código, nombres y comentarios en español cuando sea práctico.
- No modificar otros repositorios del propietario.

## Desarrollo

El proyecto se construirá en entregas pequeñas y verificables. No se considera listo para uso comercial hasta completar pruebas de reproducción, cobro, recuperación ante fallos, instalador y compatibilidad en cada Windows objetivo.
