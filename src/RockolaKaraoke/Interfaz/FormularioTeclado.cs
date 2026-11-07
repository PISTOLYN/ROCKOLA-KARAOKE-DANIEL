using System;
using System.Drawing;
using System.Windows.Forms;
using RockolaKaraoke.Servicios;

namespace RockolaKaraoke.Interfaz
{
    /// <summary>CONFIGURAR TECLADO: pestaña de teclas (doble clic para cambiar) y pestaña OTROS.</summary>
    public class FormularioTeclado : Form
    {
        private readonly ListView lista = new ListView();

        public FormularioTeclado()
        {
            Text = "CONFIGURAR TECLADO";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(900, 700);
            BackColor = Color.FromArgb(5, 14, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            TabControl pestanas = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
            TabPage paginaTeclas = new TabPage("CONFIGURAR TECLAS") { BackColor = Color.FromArgb(235, 240, 245) };
            TabPage paginaOtros = new TabPage("OTROS") { BackColor = Color.FromArgb(235, 240, 245) };
            pestanas.TabPages.Add(paginaTeclas);
            pestanas.TabPages.Add(paginaOtros);

            // ---- Pestaña 1: lista de acciones ----
            Label ayuda = new Label { Text = "SELECCIONA UNA ACCIÓN Y PULSA CAMBIAR TECLA (TAMBIÉN PUEDES DAR DOBLE CLIC O ENTER)", Dock = DockStyle.Top, Height = 34, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Black, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            lista.Dock = DockStyle.Fill; lista.View = View.Details; lista.FullRowSelect = true; lista.HideSelection = false;
            lista.MultiSelect = false; lista.Font = new Font("Segoe UI", 11F, FontStyle.Bold); lista.BackColor = Color.White; lista.ForeColor = Color.Black;
            lista.Columns.Add("ACCIÓN", 600); lista.Columns.Add("TECLA", 250);
            lista.ItemActivate += delegate { CambiarTeclaSeleccionada(); };
            // ENTER debe abrir la captura aunque la fila solo tenga el foco.
            lista.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CambiarTeclaSeleccionada();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            Panel botones = new Panel { Dock = DockStyle.Bottom, Height = 56 };
            Button cambiar = new Button { Text = "CAMBIAR TECLA SELECCIONADA", Left = 8, Top = 8, Width = 300, Height = 40 };
            Button restablecer = new Button { Text = "RESTABLECER", Left = 316, Top = 8, Width = 190, Height = 40 };
            Button cerrar = new Button { Text = "CERRAR", Left = 512, Top = 8, Width = 150, Height = 40 };
            foreach (Button b in new Button[] { cambiar, restablecer, cerrar })
            {
                b.FlatStyle = FlatStyle.Flat; b.BackColor = Color.FromArgb(0, 105, 175); b.ForeColor = Color.White;
                b.Font = new Font("Segoe UI", 10F, FontStyle.Bold); botones.Controls.Add(b);
            }
            cambiar.Click += delegate { CambiarTeclaSeleccionada(); };
            restablecer.Click += delegate { ConfiguracionTeclado.Restablecer(); LlenarLista(0); };
            cerrar.Click += delegate { Close(); };
            paginaTeclas.Controls.Add(lista); paginaTeclas.Controls.Add(botones); paginaTeclas.Controls.Add(ayuda);

            // ---- Pestaña 2: otros ----
            Label tituloVolumen = new Label { Text = "PASO DE VOLUMEN al usar las teclas de subir / bajar (1 a 20):", Left = 15, Top = 25, Width = 560, Height = 26, ForeColor = Color.Black };
            NumericUpDown paso = new NumericUpDown { Left = 590, Top = 22, Width = 80, Minimum = 1, Maximum = 20, Value = Math.Max(1, Math.Min(20, ConfiguracionTeclado.PasoVolumen)) };
            paso.ValueChanged += delegate { ConfiguracionTeclado.PasoVolumen = Decimal.ToInt32(paso.Value); ConfiguracionTeclado.Guardar(); };
            CheckBox quitaUltimo = new CheckBox { Text = "AL PRESIONAR CANCELAR, QUITAR EL ÚLTIMO TEMA DE LA LISTA DE ESPERA", Left = 15, Top = 75, Width = 800, Height = 28, Checked = ConfiguracionTeclado.CancelarQuitaUltimo, ForeColor = Color.Black };
            quitaUltimo.CheckedChanged += delegate { ConfiguracionTeclado.CancelarQuitaUltimo = quitaUltimo.Checked; ConfiguracionTeclado.Guardar(); };
            CheckBox confirmar = new CheckBox { Text = "PEDIR CONFIRMACIÓN ANTES DE CERRAR EL PROGRAMA O APAGAR LA PC", Left = 15, Top = 120, Width = 800, Height = 28, Checked = ConfiguracionTeclado.ConfirmarSalida, ForeColor = Color.Black };
            confirmar.CheckedChanged += delegate { ConfiguracionTeclado.ConfirmarSalida = confirmar.Checked; ConfiguracionTeclado.Guardar(); };
            paginaOtros.Controls.Add(tituloVolumen); paginaOtros.Controls.Add(paso);
            paginaOtros.Controls.Add(quitaUltimo); paginaOtros.Controls.Add(confirmar);

            Controls.Add(pestanas);
            KeyPreview = true;
            LlenarLista(0);
            Shown += delegate { Activate(); lista.Focus(); };
        }

        private void LlenarLista(int seleccionar)
        {
            lista.BeginUpdate();
            lista.Items.Clear();
            foreach (AccionTeclado accion in ConfiguracionTeclado.Acciones)
            {
                ListViewItem fila = new ListViewItem(ConfiguracionTeclado.Nombre(accion));
                fila.SubItems.Add(ConfiguracionTeclado.NombreTecla(ConfiguracionTeclado.Obtener(accion)));
                fila.Tag = accion;
                lista.Items.Add(fila);
            }
            lista.EndUpdate();
            if (seleccionar >= 0 && seleccionar < lista.Items.Count)
            {
                lista.Items[seleccionar].Selected = true;
                lista.Items[seleccionar].Focused = true;
                lista.Items[seleccionar].EnsureVisible();
            }
        }

        private void CambiarTeclaSeleccionada()
        {
            if (lista.SelectedItems.Count == 0) return;
            int indice = lista.SelectedIndices[0];
            AccionTeclado accion = (AccionTeclado)lista.SelectedItems[0].Tag;
            using (CapturaTecla captura = new CapturaTecla(ConfiguracionTeclado.Nombre(accion)))
            {
                if (captura.ShowDialog(this) == DialogResult.OK) ConfiguracionTeclado.Asignar(accion, captura.Tecla);
            }
            LlenarLista(indice);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
