# ALEXA - ROCKOLA KARAOKE DANIEL

Aplicación de escritorio de karaoke y rockola para Windows, desarrollada en C# / Windows Forms sobre .NET Framework 4.8.

## Menú principal y géneros dinámicos\n\n- La tecla M abre el menú de administración inspirado en la referencia visual: configurar música, configuración general, skins, teclado, estadísticas, activar, cerrar, acerca de y recomendaciones.\n- Los botones de colores de la pantalla principal quedan reservados para géneros/categorías.\n- Desde Configurar música se pueden crear géneros personalizados, elegir tipo de clasificación y asignar color (amarillo, verde, rosa, azul, naranja o celeste).\n- Los géneros y sus colores se guardan en la configuración local.\n- Clasificación inicial: Música (audio fuera de carpetas Karaoke), Karaokes (archivos dentro de carpetas con nombre Karaoke) y Videos (videos fuera de carpetas Karaoke). Los géneros personalizados filtran por carpeta con el nombre del género.\n\n## Entrega 3: biblioteca y reproducción multimedia

- Se conserva la distribución visual aprobada: marquesina, carrusel de siete miniaturas, búsqueda, categorías, lista central, cola derecha y barra inferior.
- Motor multimedia LibVLCSharp + LibVLC nativo para Windows.
- Escaneo recursivo de una carpeta seleccionada para audio (MP3, WAV, FLAC, M4A, AAC, OGG, WMA) y video (MP4, MKV, AVI, MOV, WMV, MPEG, MPG, WEBM).
- Búsqueda por título y carpeta/artista.
- Agregar a la cola con doble clic o Enter.
- Reproducir canción seleccionada en la cola, pausar, detener y avanzar automáticamente al terminar.
- Ventana de video a pantalla completa para archivos de video.
- Reordenar canciones en espera.
- Configuración para carpeta multimedia y texto de marquesina; se guarda en %AppData%\AlexaRockola\config.ini.
- Atajos: M abre el menú principal, Enter agrega, Espacio pausa, F11 pantalla completa y Esc sale de pantalla completa/detiene.\n- Se añadió un panel de portada con lectura opcional de `Assets/default-cover.jpg`; la imagen del león compartida aún debe incorporarse al repositorio en esa ruta para que aparezca como carátula predeterminada.

## Compilar

Abrir `ALEXA.sln` en Visual Studio 2022 con .NET Framework 4.8 Developer Pack y restauración NuGet habilitada. Compilar Release. El paquete nativo LibVLC Windows es grande; el primer restore puede tardar.

## Pendiente

Aún falta validar la compilación y reproducción en equipos reales. Esta entrega no incluye todavía SQLite, perfiles separados de Música/Karaoke/Video con metadatos, portadas reales, ecualizador, créditos/monedero Arduino, ni instalador final.
