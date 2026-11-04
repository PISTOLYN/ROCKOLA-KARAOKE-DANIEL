using System.Drawing;
using System.Windows.Forms;
namespace RockolaKaraoke.Interfaz
{
 public class FormularioPrincipal : Form
 {
  public FormularioPrincipal()
  {
   Text = "ROCKOLA KARAOKE DANIEL"; StartPosition = FormStartPosition.CenterScreen; WindowState = FormWindowState.Maximized; MinimumSize = new Size(900, 600); BackColor = Color.FromArgb(15, 18, 35); ForeColor = Color.White; Font = new Font("Segoe UI", 11F);
   Label titulo = new Label(); titulo.Text = "ROCKOLA  •  KARAOKE"; titulo.Dock = DockStyle.Top; titulo.Height = 100; titulo.TextAlign = ContentAlignment.MiddleCenter; titulo.Font = new Font("Segoe UI", 28F, FontStyle.Bold); titulo.ForeColor = Color.DeepSkyBlue; Controls.Add(titulo);
   Label bienvenida = new Label(); bienvenida.Text = "¡Elige tu música favorita!"; bienvenida.Dock = DockStyle.Top; bienvenida.Height = 65; bienvenida.TextAlign = ContentAlignment.MiddleCenter; bienvenida.Font = new Font("Segoe UI", 20F, FontStyle.Bold); Controls.Add(bienvenida);
   Label estado = new Label(); estado.Text = "Sistema iniciado • Base de datos local preparada"; estado.Dock = DockStyle.Bottom; estado.Height = 45; estado.TextAlign = ContentAlignment.MiddleCenter; estado.ForeColor = Color.LightGray; Controls.Add(estado);
   Button admin = new Button(); admin.Text = "ADMINISTRACIÓN"; admin.Size = new Size(230, 60); admin.Anchor = AnchorStyles.None; admin.Left = (ClientSize.Width-admin.Width)/2; admin.Top = 230; admin.FlatStyle = FlatStyle.Flat; admin.BackColor = Color.FromArgb(35, 90, 150); admin.ForeColor = Color.White; admin.Click += delegate { MessageBox.Show("El panel de administración se desarrollará en la siguiente etapa.", "Rockola Karaoke", MessageBoxButtons.OK, MessageBoxIcon.Information); }; Controls.Add(admin);
  }
 }
}
