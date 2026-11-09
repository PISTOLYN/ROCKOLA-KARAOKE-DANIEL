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
                using (PantallaInicio inicio = new PantallaInicio())
                {
                    inicio.Show();
                    inicio.Actualizar(18, "INICIALIZANDO LEON ROCKOLAS...");
                    Application.DoEvents();
                    BaseDatos.Inicializar();
                    inicio.Actualizar(58, "PREPARANDO BIBLIOTECA MUSICAL...");
                    Application.DoEvents();
                    inicio.Actualizar(82, "CARGANDO INTERFAZ...");
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(350);
                    inicio.Actualizar(100, "¡LISTO!");
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(300);
                }

                Application.Run(new FormularioReproductor());
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo iniciar la Rockola.\\n\\n" + ex.Message,
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
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 570);
            BackColor = Color.FromArgb(8, 12, 22);
            ShowInTaskbar = false;

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
