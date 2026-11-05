# ALEXA - ROCKOLA KARAOKE DANIEL

Aplicación de escritorio de Karaoke y Rockola para Windows.

## Primera entrega: interfaz base

Se incorporó una primera pantalla funcional siguiendo la referencia visual aprobada por el usuario:

- Marquesina superior verde neón con desplazamiento.
- Carrusel horizontal de siete portadas de álbum.
- Buscador de canciones y contador de álbumes/géneros.
- Botones de categorías con colores configurados.
- Lista central de canciones con selección.
- Panel derecho de lista de reproducción.
- Barra inferior roja de estado y controles.
- Agregar canciones a la cola con doble clic o Enter.
- Reordenar canciones en la cola con los controles de flechas.
- Atajos: F11 alterna pantalla completa/ventana; Esc vuelve a ventana.

## Plataforma

- C# / Windows Forms.
- .NET Framework 4.8.
- Solución Visual Studio: `ALEXA.sln`.

## Estado y alcance

Esta entrega implementa la estructura visual y las primeras interacciones de biblioteca/cola con canciones de demostración. **Todavía no incluye** el motor de reproducción, escaneo real de carpetas, miniaturas desde archivos, base de datos SQLite, configuración del engrane, monedero Arduino ni instaladores. Esas funciones se integrarán en las siguientes etapas; no se debe considerar aún un programa terminado para uso comercial.

## Compilar

Abrir `ALEXA.sln` en Visual Studio en Windows con la carga de trabajo de desarrollo de escritorio .NET. Compilar en configuración Release.

