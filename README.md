# ALEXA - ROCKOLA KARAOKE DANIEL

Aplicación de escritorio de karaoke y rockola para Windows, desarrollada en C# / Windows Forms sobre .NET Framework 4.8.

## Entrega 2: biblioteca y reproducción multimedia

- Se conserva la distribución visual aprobada: marquesina, carrusel de siete miniaturas, búsqueda, categorías, lista central, cola derecha y barra inferior.
- Motor multimedia LibVLCSharp + LibVLC nativo para Windows.
- Escaneo recursivo de una carpeta seleccionada para audio (MP3, WAV, FLAC, M4A, AAC, OGG, WMA) y video (MP4, MKV, AVI, MOV, WMV, MPEG, MPG, WEBM).
- Búsqueda por título y carpeta/artista.
- Agregar a la cola con doble clic o Enter.
- Reproducir canción seleccionada en la cola, pausar, detener y avanzar automáticamente al terminar.
- Ventana de video a pantalla completa para archivos de video.
- Reordenar canciones en espera.
- Configuración para carpeta multimedia y texto de marquesina; se guarda en %AppData%\AlexaRockola\config.ini.
- Atajos: Enter agrega, Espacio pausa, F11 pantalla completa y Esc sale de pantalla completa/detiene.

## Compilar

Abrir `ALEXA.sln` en Visual Studio 2022 con .NET Framework 4.8 Developer Pack y restauración NuGet habilitada. Compilar Release. El paquete nativo LibVLC Windows es grande; el primer restore puede tardar.

## Pendiente

Aún falta validar la compilación y reproducción en equipos reales. Esta entrega no incluye todavía SQLite, perfiles separados de Música/Karaoke/Video con metadatos, portadas reales, ecualizador, créditos/monedero Arduino, ni instalador final.
