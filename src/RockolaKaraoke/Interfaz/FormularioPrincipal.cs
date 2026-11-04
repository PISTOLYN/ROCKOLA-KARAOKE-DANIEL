using System.Drawing;
using System.Windows.Forms;

namespace RockolaKaraoke.Interfaz
{
    public class FormularioPrincipal : Form
    {
        public FormularioPrincipal()
        {
            Text = "ROCKOLA KARAOKE DANIEL";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(900, 600);
            BackColor = Color.FromArgb(15, 18, 35);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 11F);

            Label titulo = new Label { Text = "ROCKOLA  •  KARAOKE", Dock = DockStyle.Top, Height = 100, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 28F, FontStyle.Bold), ForeColor = Color.DeepSkyBlue };
            Controls.Add(titulo);
            Label bienvenida = new Label { Text = "¡Elige tu música favorita!", Dock = DockStyle.Top, Height = 65, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 20F, FontStyle.Bold) };
            Controls.Add(bienvenida);
            Label estado = new Label { Text = "Sistema iniciado • Base de datos local preparada", Dock = DockStyle.Bottom, Height = 45, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightGray };
            Controls.Add(estado);

            Button reproductor = CrearBoton("ABRIR REPRODUCTOR", 230);
            reproductor.Top = 220;
            reproductor.Click += delegate { using (FormularioReproductor ventana = new FormularioReproductor()) { ventana.ShowDialog(this); } };
            Controls.Add(reproductor);

            Button admin = CrearBoton("ADMINISTRACIÓN", 230);
            admin.Top = 300;
            admin.Click += delegate { using (FormularioBiblioteca ventana = new FormularioBiblioteca()) { ventana.ShowDialog(this); } };
            Controls.Add(admin);

            Resize += delegate
            {
                reproductor.Left = (ClientSize.Width - reproductor.Width) / 2;
                admin.Left = (ClientSize.Width - admin.Width) / 2;
            };
        }

        private Button CrearBoton(string texto, int ancho)
        {
            return new Button
            {
                Text = texto,
                Size = new Size(ancho, 60),
                Left = (ClientSize.Width - ancho) / 2,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(35, 90, 150),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
        }
    }
}