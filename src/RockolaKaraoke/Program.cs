using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RockolaKaraoke.Datos;
using RockolaKaraoke.Interfaz;

namespace RockolaKaraoke
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                FormularioReproductor reproductor = null;
                using (PantallaInicio inicio = new PantallaInicio())
                {
                    inicio.Show();
                    inicio.Actualizar(8, "INICIALIZANDO LEON ROCKOLAS...");
                    Application.DoEvents();

                    BaseDatos.Inicializar();
                    inicio.Actualizar(35, "PREPARANDO BASE DE DATOS...");
                    Application.DoEvents();

                    inicio.Actualizar(55, "CARGANDO BIBLIOTECA MUSICAL...");
                    Application.DoEvents();

                    // Construir la interfaz y cargar sus datos antes de cerrar
                    // la pantalla de inicio: el 100% significa que ya está lista.
                    inicio.Actualizar(78, "PREPARANDO INTERFAZ Y CONTROLES...");
                    Application.DoEvents();
                    reproductor = new FormularioReproductor();

                    inicio.Actualizar(96, "FINALIZANDO CARGA...");
                    Application.DoEvents();

                    // Mostrar el reproductor mientras la pantalla de inicio sigue
                    // encima. Esto fuerza a WinForms a ejecutar Load/Shown y termina
                    // la configuración visual antes de retirar la pantalla del león.
                    reproductor.Show();
                    reproductor.Refresh();
                    Application.DoEvents();

                    inicio.Actualizar(100, "¡LEON ROCKOLAS ESTA LISTO!");
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(700);
                }

                reproductor.Activate();
                Application.Run(reproductor);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo iniciar la Rockola.\n\n" + ex.Message,
                    "Error de inicio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal sealed class PantallaInicio : Form
    {
        private readonly ProgressBar progreso = new ProgressBar();
        private readonly Label estado = new Label();

        public PantallaInicio()
        {
            Text = "LEON ROCKOLAS";
            FormBorderStyle = FormBorderStyle.None;
            // Fijar directamente el tamaño de pantalla evita la animación
            // de maximizar/restaurar que hace que la portada de inicio "brinque".
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromPoint(Cursor.Position).Bounds;
            MinimumSize = Size;
            MaximumSize = Size;
            BackColor = Color.FromArgb(8, 12, 22);
            ShowInTaskbar = false;
            ShowIcon = false;
            // No usar TopMost: podría ocultar un aviso modal del reproductor\n            // y dar la impresión de que la aplicación se quedó en la carga.

            PictureBox leon = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(8, 12, 22)
            };
            string ruta = Path.Combine(Application.StartupPath, "LeonDefault.jpg.jpg");
            if (File.Exists(ruta))
            {
                using (Image original = Image.FromFile(ruta))
                    leon.Image = new Bitmap(original);
            }

            Panel pie = new Panel { Dock = DockStyle.Bottom, Height = 92, BackColor = Color.FromArgb(8, 12, 22), Padding = new Padding(34, 8, 34, 12) };
            Label marca = new Label
            {
                Text = "LEON ROCKOLAS",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.FromArgb(226, 194, 133),
                Font = new Font("Georgia", 16F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            estado.Dock = DockStyle.Top;
            estado.Height = 22;
            estado.ForeColor = Color.Gainsboro;
            estado.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            estado.TextAlign = ContentAlignment.MiddleCenter;
            progreso.Dock = DockStyle.Bottom;
            progreso.Height = 18;
            progreso.Minimum = 0;
            progreso.Maximum = 100;
            pie.Controls.Add(progreso);
            pie.Controls.Add(estado);
            pie.Controls.Add(marca);
            Controls.Add(leon);
            Controls.Add(pie);
        }

        public void Actualizar(int valor, string mensaje)
        {
            progreso.Value = Math.Max(0, Math.Min(100, valor));
            estado.Text = mensaje;
            Refresh();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Controls.Count > 0)
            {
                foreach (Control control in Controls)
                {
                    PictureBox imagen = control as PictureBox;
                    if (imagen != null && imagen.Image != null)
                    {
                        imagen.Image.Dispose();
                        imagen.Image = null;
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
