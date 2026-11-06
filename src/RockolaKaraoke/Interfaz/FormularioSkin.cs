using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RockolaKaraoke.Servicios;

namespace RockolaKaraoke.Interfaz
{
    public class FormularioSkin : Form
    {
        Button fondo = ColorBtn("FONDO", ConfiguracionSkin.Fondo), panel = ColorBtn("PANELES", ConfiguracionSkin.Panel), acento = ColorBtn("BOTONES", ConfiguracionSkin.Acento), texto = ColorBtn("TEXTO", ConfiguracionSkin.Texto);
        TextBox imagen = new TextBox(), mensajes = new TextBox();
        ComboBox resolucion = new ComboBox();
        ComboBox listaSkins = new ComboBox();
        NumericUpDown tamano = new NumericUpDown();
        Panel muestra = new Panel(); Label titulo = new Label(), mensaje = new Label();

        static Button ColorBtn(string s, Color c) { return new Button { Text = s, BackColor = c, ForeColor = Color.White, Width = 180, Height = 42, FlatStyle = FlatStyle.Flat }; }

        public FormularioSkin()
        {
            Text = "PERSONALIZAR SKIN"; StartPosition = FormStartPosition.CenterParent; Size = new Size(900, 780); MinimumSize = new Size(820, 720); BackColor = Color.FromArgb(12, 16, 31); ForeColor = Color.White; Font = new Font("Segoe UI", 10);
            Panel head = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(23, 30, 53) }; 
            head.Controls.Add(new Label { Text = "PERSONALIZAR SKIN", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DeepSkyBlue, Font = new Font("Segoe UI", 20, FontStyle.Bold) });
            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), AutoScroll = true }; 
            int y = 10;

            // --- SELECTOR DE SKINS ---
            body.Controls.Add(new Label { Text = "Selecciona una Carpeta de Skin:", Bounds = new Rectangle(20, y, 300, 25) }); y += 32;
            listaSkins.SetBounds(20, y, 300, 30); listaSkins.DropDownStyle = ComboBoxStyle.DropDownList;
            
            string rutaSkins = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skins");
            if (Directory.Exists(rutaSkins))
            {
                foreach (string carpeta in Directory.GetDirectories(rutaSkins))
                {
                    listaSkins.Items.Add(Path.GetFileName(carpeta));
                }
            }
            if (listaSkins.Items.Count > 0)
            {
                listaSkins.SelectedItem = ConfiguracionSkin.SkinActivo;
                if (listaSkins.SelectedIndex < 0) listaSkins.SelectedIndex = 0;
            }
            body.Controls.Add(listaSkins); y += 45;

            // --- COLORES ---
            Label l = new Label { Text = "Colores de la interfaz", Bounds = new Rectangle(20, y, 300, 25) }; body.Controls.Add(l); y += 32;
            Button[] bs = { fondo, panel, acento, texto }; 
            for (int i = 0; i < bs.Length; i++) { Button actual = bs[i]; actual.SetBounds(20 + i * 205, y, 190, 42); body.Controls.Add(actual); actual.Click += delegate { Elegir(actual); }; } y += 55;

            // --- IMAGEN SUELTA ---
            body.Controls.Add(new Label { Text = "Imagen de fondo (opcional si no usas carpeta Skins)", Bounds = new Rectangle(20, y, 350, 25) }); y += 25;
            imagen.SetBounds(20, y, 650, 30); imagen.Text = ConfiguracionSkin.Imagen; body.Controls.Add(imagen);
            Button buscar = new Button { Text = "BUSCAR", Bounds = new Rectangle(680, y, 150, 30) }; body.Controls.Add(buscar);
            buscar.Click += delegate { using (OpenFileDialog d = new OpenFileDialog { Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp" }) if (d.ShowDialog(this) == DialogResult.OK) imagen.Text = d.FileName; }; y += 45;

            // --- RESOLUCIÓN Y TAMAÑO ---
            body.Controls.Add(new Label { Text = "Resolución", Bounds = new Rectangle(20, y, 100, 30) });
            resolucion.SetBounds(130, y, 220, 30); resolucion.DropDownStyle = ComboBoxStyle.DropDownList; resolucion.Items.AddRange(new object[] { "Pantalla completa", "1024x768", "1280x720", "1366x768", "1600x900", "1920x1080" }); resolucion.SelectedItem = ConfiguracionSkin.Resolucion; if (resolucion.SelectedIndex < 0) resolucion.SelectedIndex = 0; body.Controls.Add(resolucion);
            body.Controls.Add(new Label { Text = "Tamaño título", Bounds = new Rectangle(400, y, 110, 30) }); tamano.SetBounds(515, y, 75, 30); tamano.Minimum = 16; tamano.Maximum = 48; tamano.Value = ConfiguracionSkin.TamanoTitulo; body.Controls.Add(tamano); y += 42;

            // --- MENSAJES ---
            body.Controls.Add(new Label { Text = "Mensajes rotativos (uno por línea)", Bounds = new Rectangle(20, y, 350, 25) }); y += 25;
            mensajes.SetBounds(20, y, 810, 65); mensajes.Multiline = true; mensajes.ScrollBars = ScrollBars.Vertical; mensajes.Text = ConfiguracionSkin.Mensajes.Replace("|", Environment.NewLine); body.Controls.Add(mensajes); y += 78;

            // --- VISTA PREVIA ---
            body.Controls.Add(new Label { Text = "Vista previa", Bounds = new Rectangle(20, y, 200, 25) }); y += 25;
            muestra.SetBounds(20, y, 810, 120); muestra.BackColor = ConfiguracionSkin.Fondo; muestra.BorderStyle = BorderStyle.FixedSingle;
            titulo.Text = "ROCKOLA • KARAOKE"; titulo.Dock = DockStyle.Top; titulo.Height = 60; titulo.TextAlign = ContentAlignment.MiddleCenter;
            mensaje.Dock = DockStyle.Fill; mensaje.TextAlign = ContentAlignment.MiddleCenter; mensaje.Text = "¡Elige tu música favorita!"; muestra.Controls.Add(mensaje); muestra.Controls.Add(titulo); body.Controls.Add(muestra); y += 132;

            // --- GUARDAR ---
            Button guardar = new Button { Text = "GUARDAR Y APLICAR SKIN", Bounds = new Rectangle(20, y, 260, 48), BackColor = Color.FromArgb(30, 105, 170), ForeColor = Color.White };
            body.Controls.Add(guardar);
            guardar.Click += delegate {
                ConfiguracionSkin.GuardarTodo(fondo.BackColor, panel.BackColor, acento.BackColor, texto.BackColor, imagen.Text, resolucion.Text, mensajes.Text.Replace(Environment.NewLine, "|"), (int)tamano.Value);
                if (listaSkins.SelectedItem != null) ConfiguracionSkin.GuardarSkinActivo(listaSkins.SelectedItem.ToString());
                DialogResult = DialogResult.OK; Close();
            };

            tamano.ValueChanged += delegate { ActualizarVista(); }; mensajes.TextChanged += delegate { ActualizarVista(); };
            Controls.Add(body); Controls.Add(head); ActualizarVista();
        }

        void Elegir(Button b) { using (ColorDialog d = new ColorDialog { Color = b.BackColor, FullOpen = true }) if (d.ShowDialog(this) == DialogResult.OK) b.BackColor = d.Color; ActualizarVista(); }
        void ActualizarVista() { muestra.BackColor = fondo.BackColor; titulo.BackColor = panel.BackColor; mensaje.BackColor = panel.BackColor; titulo.ForeColor = texto.BackColor; mensaje.ForeColor = texto.BackColor; titulo.Font = new Font("Segoe UI", (float)tamano.Value, FontStyle.Bold); mensaje.Text = mensajes.Lines.Length > 0 ? mensajes.Lines[0] : "Vista previa"; }
    }
}
