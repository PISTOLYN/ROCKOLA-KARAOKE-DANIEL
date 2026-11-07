using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace RockolaKaraoke.Servicios
{
    public enum AccionTeclado
    {
        Arriba, Abajo, Izquierda, Derecha, Agregar, Cancelar, SiguienteTema, CambiarModo, Menu,
        CerrarPrograma, SubirVolumen, BajarVolumen, PantallaCompleta, ApagarPC, Pausa, ReproducirTema,
        Abecedario, MostrarOcultarCola, Buscar
    }

    /// <summary>Teclas configurables para controlar la rockola sin mouse, y opciones varias.</summary>
    public static class ConfiguracionTeclado
    {
        private static readonly Dictionary<AccionTeclado, Keys> teclas = new Dictionary<AccionTeclado, Keys>();

        public static bool CancelarQuitaUltimo = true;
        public static bool ConfirmarSalida = true;
        public static int PasoVolumen = 5;
        public static bool MostrarMensajeAlAgregar = true;
        public static bool MostrarListaGeneros = true;
        public static bool IrSiguienteGeneroDirectamente = false;
        public static bool MostrarAbecedarioAlMantenerFlecha = true;
        public static int TiempoEsperaAbecedario = 2;

        static ConfiguracionTeclado()
        {
            RestablecerTeclas();
            Cargar();
        }

        private static string Archivo
        {
            get { return Path.Combine(Application.UserAppDataPath, "teclado.txt"); }
        }

        public static AccionTeclado[] Acciones
        {
            get { return (AccionTeclado[])Enum.GetValues(typeof(AccionTeclado)); }
        }

        public static string Nombre(AccionTeclado accion)
        {
            switch (accion)
            {
                case AccionTeclado.Arriba: return "SUBIR EN LA LISTA DE TEMAS";
                case AccionTeclado.Abajo: return "BAJAR EN LA LISTA DE TEMAS";
                case AccionTeclado.Izquierda: return "IR A LA IZQUIERDA EN LA LISTA DE ÁLBUMES";
                case AccionTeclado.Derecha: return "IR A LA DERECHA EN LA LISTA DE ÁLBUMES";
                case AccionTeclado.Agregar: return "OK (AGREGAR TEMA A LA LISTA DE ESPERA)";
                case AccionTeclado.Cancelar: return "CANCELAR (QUITAR ÚLTIMO TEMA / CERRAR)";
                case AccionTeclado.SiguienteTema: return "IR AL SIGUIENTE TEMA DE LA LISTA DE REPRODUCCIÓN";
                case AccionTeclado.CambiarModo: return "CAMBIAR GÉNERO MUSICAL (KARAOKE / MÚSICA / VIDEO)";
                case AccionTeclado.Menu: return "MOSTRAR MENÚ DE CONFIGURACIÓN";
                case AccionTeclado.CerrarPrograma: return "CERRAR EL PROGRAMA";
                case AccionTeclado.SubirVolumen: return "SUBIR VOLUMEN";
                case AccionTeclado.BajarVolumen: return "BAJAR VOLUMEN";
                case AccionTeclado.PantallaCompleta: return "IR A PANTALLA COMPLETA";
                case AccionTeclado.ApagarPC: return "APAGAR LA COMPUTADORA";
                case AccionTeclado.Pausa: return "PAUSA";
                case AccionTeclado.ReproducirTema: return "REPRODUCIR TEMA";
                case AccionTeclado.Abecedario: return "MOSTRAR EL MENÚ ABECEDARIO";
                case AccionTeclado.MostrarOcultarCola: return "MOSTRAR / OCULTAR LISTA DE REPRODUCCIÓN";
                default: return "BUSCAR TEMAS";
            }
        }

        public static Keys Obtener(AccionTeclado accion) { return teclas[accion]; }

        public static string NombreTecla(Keys tecla)
        {
            switch (tecla)
            {
                case Keys.Add: return "TECLA DE ADICIÓN (+)";
                case Keys.Subtract: return "TECLA DE SUSTRACCIÓN (-)";
                case Keys.Divide: return "TECLA DE DIVISIÓN (/)";
                case Keys.Multiply: return "TECLA DE MULTIPLICACIÓN (*)";
                case Keys.Return: return "ENTER";
                case Keys.Escape: return "ESC";
                default: return tecla.ToString().ToUpperInvariant();
            }
        }

        public static AccionTeclado? Buscar(Keys teclaPresionada)
        {
            // Comparar por código base, ignorando modificadores y evitando que
            // Keys.None coincida accidentalmente con alguna acción.
            Keys tecla = teclaPresionada & Keys.KeyCode;
            if (tecla == Keys.None) return null;

            foreach (AccionTeclado accion in Acciones)
            {
                Keys asignada = teclas[accion] & Keys.KeyCode;
                if (asignada != Keys.None && asignada == tecla) return accion;
            }
            return null;
        }

        /// <summary>Asigna una tecla. Si otra acción ya la usaba, intercambian teclas.</summary>
        public static void Asignar(AccionTeclado accion, Keys tecla)
        {
            tecla &= Keys.KeyCode;
            if (tecla == Keys.None) return;

            Keys anterior = teclas[accion] & Keys.KeyCode;
            foreach (AccionTeclado otra in Acciones)
            {
                if (otra == accion) continue;
                if ((teclas[otra] & Keys.KeyCode) == tecla)
                {
                    teclas[otra] = anterior;
                    break;
                }
            }

            teclas[accion] = tecla;
            Guardar();
        }

        public static void Restablecer()
        {
            RestablecerTeclas();
            Guardar();
        }

        private static void RestablecerTeclas()
        {
            teclas[AccionTeclado.Arriba] = Keys.Up;
            teclas[AccionTeclado.Abajo] = Keys.Down;
            teclas[AccionTeclado.Izquierda] = Keys.Left;
            teclas[AccionTeclado.Derecha] = Keys.Right;
            teclas[AccionTeclado.Agregar] = Keys.Enter;
            teclas[AccionTeclado.Cancelar] = Keys.Escape;
            teclas[AccionTeclado.SiguienteTema] = Keys.N;
            teclas[AccionTeclado.CambiarModo] = Keys.Tab;
            teclas[AccionTeclado.Menu] = Keys.M;
            teclas[AccionTeclado.CerrarPrograma] = Keys.End;
            teclas[AccionTeclado.SubirVolumen] = Keys.Add;
            teclas[AccionTeclado.BajarVolumen] = Keys.Subtract;
            teclas[AccionTeclado.PantallaCompleta] = Keys.F11;
            teclas[AccionTeclado.ApagarPC] = Keys.Divide;
            teclas[AccionTeclado.Pausa] = Keys.P;
            teclas[AccionTeclado.ReproducirTema] = Keys.R;
            teclas[AccionTeclado.Abecedario] = Keys.A;
            teclas[AccionTeclado.MostrarOcultarCola] = Keys.L;
            teclas[AccionTeclado.Buscar] = Keys.B;
        }

        public static void Guardar()
        {
            try
            {
                List<string> lineas = new List<string>();
                foreach (AccionTeclado a in Acciones) lineas.Add(a + "=" + teclas[a]);
                lineas.Add("Otros.CancelarQuitaUltimo=" + (CancelarQuitaUltimo ? "1" : "0"));
                lineas.Add("Otros.ConfirmarSalida=" + (ConfirmarSalida ? "1" : "0"));
                lineas.Add("Otros.PasoVolumen=" + PasoVolumen);
                lineas.Add("Musica.MostrarMensajeAlAgregar=" + (MostrarMensajeAlAgregar ? "1" : "0"));
                lineas.Add("Musica.MostrarListaGeneros=" + (MostrarListaGeneros ? "1" : "0"));
                lineas.Add("Musica.IrSiguienteGeneroDirectamente=" + (IrSiguienteGeneroDirectamente ? "1" : "0"));
                lineas.Add("Musica.MostrarAbecedarioAlMantenerFlecha=" + (MostrarAbecedarioAlMantenerFlecha ? "1" : "0"));
                lineas.Add("Musica.TiempoEsperaAbecedario=" + TiempoEsperaAbecedario);
                File.WriteAllLines(Archivo, lineas.ToArray());
            }
            catch { }
        }

        private static void Cargar()
        {
            try
            {
                if (!File.Exists(Archivo)) return;
                foreach (string linea in File.ReadAllLines(Archivo))
                {
                    string[] partes = linea.Split('=');
                    if (partes.Length != 2) continue;
                    string clave = partes[0].Trim();
                    string valor = partes[1].Trim();
                    try
                    {
                        if (clave == "Otros.CancelarQuitaUltimo") CancelarQuitaUltimo = valor == "1";
                        else if (clave == "Otros.ConfirmarSalida") ConfirmarSalida = valor == "1";
                        else if (clave == "Otros.PasoVolumen") PasoVolumen = Math.Max(1, Math.Min(20, Int32.Parse(valor)));
                        else if (clave == "Musica.MostrarMensajeAlAgregar") MostrarMensajeAlAgregar = valor == "1";
                        else if (clave == "Musica.MostrarListaGeneros") MostrarListaGeneros = valor == "1";
                        else if (clave == "Musica.IrSiguienteGeneroDirectamente") IrSiguienteGeneroDirectamente = valor == "1";
                        else if (clave == "Musica.MostrarAbecedarioAlMantenerFlecha") MostrarAbecedarioAlMantenerFlecha = valor == "1";
                        else if (clave == "Musica.TiempoEsperaAbecedario") TiempoEsperaAbecedario = Math.Max(1, Math.Min(15, Int32.Parse(valor)));
                        else
                        {
                            AccionTeclado accion = (AccionTeclado)Enum.Parse(typeof(AccionTeclado), clave);
                            Keys tecla = (Keys)Enum.Parse(typeof(Keys), valor);
                            teclas[accion] = tecla;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
