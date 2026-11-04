using System;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using RockolaKaraoke.Servicios;

namespace RockolaKaraoke.Interfaz
{
    public class FormularioReproductor : Form
    {
        private readonly ListView biblioteca = new ListView();
        private readonly System.Collections.Generic.List<ListViewItem> todosLosElementos = new System.Collections.Generic.List<ListViewItem>();
        private readonly FlowLayoutPanel mosaico = new FlowLayoutPanel();
        private readonly TextBox busquedaMosaico = new TextBox();
        private string filtroTipo = "Todos";
        private readonly ListView cola = new ListView();
        private readonly Label etiquetaCola = new Label();
        private readonly VideoView pantalla = new VideoView();
        private readonly Label tituloActual = new Label();
        private readonly Label tiempo = new Label();
        private readonly TrackBar progreso = new TrackBar();
        private readonly TrackBar volumen = new TrackBar();
        private readonly Button reproducir = new Button();
        private readonly Button anterior = new Button();
        private readonly Button siguiente = new Button();
        private readonly Button detener = new Button();
        private readonly Timer reloj = new Timer();
        private LibVLC motor;
        private MediaPlayer reproductor;
        private Media medioActual;
        private bool moviendoProgreso;

        public FormularioReproductor()
        {
            Text = "Reproductor multimedia - Rockola Karaoke Daniel";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1050, 680);
            BackColor = Color.FromArgb(12, 16, 31);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            KeyPreview = true;
            CrearInterfaz();
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.F12)
                {
                    e.SuppressKeyPress = true;
                    MostrarAdministracion();
                }
            };
            Load += delegate { InicializarMotor(); CargarBiblioteca(); };
            FormClosing += delegate { LiberarMotor(); };
            reloj.Interval = 500;
            reloj.Tick += Reloj_Tick;
        }

        private void MostrarAdministracion()
        {
            using (Form menu = new Form())
            {
                menu.Text = "Administración - Rockola Karaoke Daniel";
                menu.StartPosition = FormStartPosition.CenterParent;
                menu.FormBorderStyle = FormBorderStyle.FixedDialog;
                menu.MaximizeBox = false;
                menu.MinimizeBox = false;
                menu.ClientSize = new Size(430, 260);
                menu.BackColor = Color.FromArgb(5, 14, 32);
                menu.ForeColor = Color.White;

                Label titulo = new Label { Text = "PANEL DE ADMINISTRACIÓN", Dock = DockStyle.Top, Height = 55, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 220, 235) };
                Button bibliotecaAdmin = new Button { Text = "BIBLIOTECA Y ESCANEO DE MÚSICA", Width = 340, Height = 45, Left = 45, Top = 75 };
                Button skinAdmin = new Button { Text = "CONFIGURAR SKIN", Width = 340, Height = 45, Left = 45, Top = 130 };
                Button cerrar = new Button { Text = "CERRAR", Width = 340, Height = 38, Left = 45, Top = 190 };
                foreach (Button b in new Button[] { bibliotecaAdmin, skinAdmin, cerrar })
                {
                    Estilizar(b);
                    b.BackColor = Color.FromArgb(0, 100, 165);
                    b.ForeColor = Color.White;
                }
                bibliotecaAdmin.Click += delegate
                {
                    using (FormularioBiblioteca ventana = new FormularioBiblioteca())
                        ventana.ShowDialog(menu);
                    CargarBiblioteca();
                };
                skinAdmin.Click += delegate
                {
                    using (FormularioSkin ventana = new FormularioSkin())
                        ventana.ShowDialog(menu);
                };
                cerrar.Click += delegate { menu.Close(); };
                menu.Controls.Add(titulo);
                menu.Controls.Add(bibliotecaAdmin);
                menu.Controls.Add(skinAdmin);
                menu.Controls.Add(cerrar);
                menu.ShowDialog(this);
            }
        }

        private void CrearInterfaz()
        {
            Panel encabezado = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.FromArgb(0, 190, 210) };
            Label marca = new Label { Text = "ROCKOLA KARAOKE  •  ELIGE TU MÚSICA FAVORITA", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI", 19F, FontStyle.Bold) };
            encabezado.Controls.Add(marca);

            Panel inferior = new Panel { Dock = DockStyle.Bottom, Height = 78, BackColor = Color.FromArgb(5, 10, 24) };
            tituloActual.Text = "No hay tema en reproducción";
            tituloActual.Dock = DockStyle.Top; tituloActual.Height = 22; tituloActual.TextAlign = ContentAlignment.MiddleLeft;
            tituloActual.Padding = new Padding(12, 0, 0, 0); tituloActual.ForeColor = Color.White; tituloActual.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            progreso.Dock = DockStyle.Top; progreso.Minimum = 0; progreso.Maximum = 1000; progreso.TickStyle = TickStyle.None; progreso.Height = 15;
            progreso.Scroll += delegate { if (reproductor != null && reproductor.IsSeekable && reproductor.Length > 0) reproductor.Position = progreso.Value / 1000f; };
            tiempo.Text = "00:00 / 00:00"; tiempo.Dock = DockStyle.Right; tiempo.Width = 110; tiempo.TextAlign = ContentAlignment.MiddleCenter; tiempo.ForeColor = Color.LightGray;
            FlowLayoutPanel controles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(10, 2, 5, 2), BackColor = Color.FromArgb(5, 10, 24) };
            anterior.Text = "◀ ANTERIOR"; reproducir.Text = "▶ REPRODUCIR"; siguiente.Text = "SIGUIENTE ▶"; detener.Text = "■ DETENER";
            foreach (Button b in new Button[] { anterior, reproducir, siguiente, detener }) { b.Size = new Size(132, 36); Estilizar(b); controles.Controls.Add(b); }
            Label etiquetaVolumen = new Label { Text = "VOLUMEN", Width = 72, Height = 36, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White };
            volumen.Minimum = 0; volumen.Maximum = 100; volumen.Value = 70; volumen.Width = 115; volumen.Height = 36; volumen.TickFrequency = 10;
            controles.Controls.Add(etiquetaVolumen); controles.Controls.Add(volumen);
            anterior.Click += delegate { ReproducirAnterior(); };
            siguiente.Click += delegate { ReproducirSiguiente(); };
            reproducir.Click += delegate
            {
                if (reproductor != null && reproductor.State == VLCState.Playing) { reproductor.Pause(); reproducir.Text = "▶ CONTINUAR"; }
                else if (reproductor != null && reproductor.State == VLCState.Paused) { reproductor.Pause(); reproducir.Text = "Ⅱ PAUSAR"; }
                else ReproducirSeleccion();
            };
            detener.Click += delegate { if (reproductor != null) reproductor.Stop(); reproducir.Text = "▶ REPRODUCIR"; };
            volumen.Scroll += delegate { if (reproductor != null) reproductor.Volume = volumen.Value; };
            inferior.Controls.Add(controles); inferior.Controls.Add(tiempo); inferior.Controls.Add(progreso); inferior.Controls.Add(tituloActual);

            SplitContainer division = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 900, BackColor = Color.Black, FixedPanel = FixedPanel.Panel2, SplitterWidth = 7 };
            Panel lado = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 12, 30) };

            mosaico.Dock = DockStyle.Top; mosaico.Height = 224; mosaico.AutoScroll = true; mosaico.WrapContents = false;
            mosaico.FlowDirection = FlowDirection.LeftToRight; mosaico.Padding = new Padding(8, 7, 8, 5);
            mosaico.BackColor = Color.FromArgb(3, 10, 27);

            FlowLayoutPanel filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 5, 8, 3), WrapContents = false, BackColor = Color.FromArgb(0, 185, 205) };
            foreach (string tipo in new string[] { "Todos", "Karaoke", "Musica", "Video" })
            {
                string etiqueta = tipo == "Musica" ? "MÚSICA" : tipo.ToUpperInvariant();
                Button filtro = new Button { Text = etiqueta, Tag = tipo, Width = 132, Height = 36 };
                Estilizar(filtro); filtro.BackColor = tipo == "Karaoke" ? Color.FromArgb(245, 190, 0) : Color.FromArgb(0, 115, 190);
                filtro.ForeColor = Color.White;
                filtro.Click += delegate(object sender, EventArgs e) { filtroTipo = Convert.ToString(((Button)sender).Tag); ConstruirMosaico(); };
                filtros.Controls.Add(filtro);
            }
            busquedaMosaico.Dock = DockStyle.Top; busquedaMosaico.Height = 34;
            busquedaMosaico.Font = new Font("Segoe UI", 11F); busquedaMosaico.Margin = new Padding(8);
            busquedaMosaico.ForeColor = Color.DimGray; busquedaMosaico.Text = "Buscar canción o artista...";
            busquedaMosaico.GotFocus += delegate { if (busquedaMosaico.Text == "Buscar canción o artista...") { busquedaMosaico.Text = ""; busquedaMosaico.ForeColor = Color.Black; } };
            busquedaMosaico.LostFocus += delegate { if (String.IsNullOrWhiteSpace(busquedaMosaico.Text)) { busquedaMosaico.Text = "Buscar canción o artista..."; busquedaMosaico.ForeColor = Color.DimGray; } };
            busquedaMosaico.TextChanged += delegate { ConstruirMosaico(); };

            biblioteca.Visible = true; biblioteca.Dock = DockStyle.Fill; biblioteca.View = View.Details;
            biblioteca.FullRowSelect = true; biblioteca.HideSelection = false; biblioteca.GridLines = false;
            biblioteca.BackColor = Color.FromArgb(2, 7, 17); biblioteca.ForeColor = Color.White;
            biblioteca.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            biblioteca.Columns.Clear(); biblioteca.Columns.Add("LISTA DE CANCIONES", 620); biblioteca.Columns.Add("ARTISTA", 260);
            biblioteca.OwnerDraw = true;
            biblioteca.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e) { e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(0, 175, 205)), e.Bounds); TextRenderer.DrawText(e.Graphics, e.Header.Text, new Font("Segoe UI", 10F, FontStyle.Bold), e.Bounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left); };
            biblioteca.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { e.DrawDefault = true; };
            biblioteca.DoubleClick += delegate { AgregarSeleccionALaCola(); };
            biblioteca.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
            {
                Color fondo = e.Item.Selected ? Color.FromArgb(245, 195, 0) : (e.ItemIndex % 2 == 0 ? Color.FromArgb(5, 15, 30) : Color.FromArgb(10, 22, 39));
                Color tinta = e.Item.Selected ? Color.Black : Color.White;
                using (SolidBrush b = new SolidBrush(fondo)) e.Graphics.FillRectangle(b, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, biblioteca.Font, e.Bounds, tinta, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };

            lado.Controls.Add(biblioteca); lado.Controls.Add(filtros); lado.Controls.Add(busquedaMosaico); lado.Controls.Add(mosaico);

            Panel panelDerecho = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(2, 7, 17), Padding = new Padding(7) };
            Panel panelPublicidad = new Panel { Dock = DockStyle.Top, Height = 245, BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle };
            pantalla.Dock = DockStyle.Fill; pantalla.BackColor = Color.Black;
            Label publicidad = new Label { Text = "AGREGUE PUBLICIDAD AQUÍ", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gold, Font = new Font("Segoe UI", 13F, FontStyle.Bold) };
            panelPublicidad.Controls.Add(pantalla); panelPublicidad.Controls.Add(publicidad);
            FlowLayoutPanel botonesModo = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(2, 6, 2, 2), WrapContents = false, BackColor = Color.FromArgb(0, 175, 200) };
            foreach (string modo in new string[] { "AUDIO", "VIDEO", "KARAOKE" })
            {
                Button b = new Button { Text = modo, Width = 96, Height = 38 };
                Estilizar(b); b.BackColor = modo == "KARAOKE" ? Color.FromArgb(245, 190, 0) : Color.FromArgb(0, 105, 175);
                botonesModo.Controls.Add(b);
                b.Click += delegate { filtroTipo = modo == "AUDIO" ? "Musica" : modo == "VIDEO" ? "Video" : "Karaoke"; ConstruirMosaico(); };
            }

            // Controles de navegación y reproducción al estilo de la rockola de referencia.
            FlowLayoutPanel controlesRockola = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(5, 5, 2, 2), WrapContents = true, BackColor = Color.FromArgb(3, 10, 24) };
            Button btnAnteriorRockola = new Button { Text = "◀", Width = 48, Height = 38, Font = new Font("Segoe UI", 17F, FontStyle.Bold) };
            Button btnPlayRockola = new Button { Text = "▶", Width = 58, Height = 38, Font = new Font("Segoe UI", 17F, FontStyle.Bold) };
            Button btnSiguienteRockola = new Button { Text = "▶|", Width = 48, Height = 38, Font = new Font("Segoe UI", 15F, FontStyle.Bold) };
            Button btnSubirRockola = new Button { Text = "▲", Width = 48, Height = 38, Font = new Font("Segoe UI", 15F, FontStyle.Bold) };
            Button btnBajarRockola = new Button { Text = "▼", Width = 48, Height = 38, Font = new Font("Segoe UI", 15F, FontStyle.Bold) };
            Button btnBuscarRockola = new Button { Text = "⌕", Width = 48, Height = 38, Font = new Font("Segoe UI", 19F, FontStyle.Bold) };
            foreach (Button b in new Button[] { btnAnteriorRockola, btnPlayRockola, btnSiguienteRockola, btnSubirRockola, btnBajarRockola, btnBuscarRockola })
            {
                Estilizar(b); b.BackColor = Color.FromArgb(0, 105, 175); b.FlatAppearance.BorderColor = Color.FromArgb(0, 220, 245);
                controlesRockola.Controls.Add(b);
            }
            btnAnteriorRockola.Click += delegate { ReproducirAnterior(); };
            btnPlayRockola.Click += delegate { reproducir.PerformClick(); btnPlayRockola.Text = reproductor != null && reproductor.State == VLCState.Playing ? "Ⅱ" : "▶"; };
            btnSiguienteRockola.Click += delegate { ReproducirSiguiente(); };
            btnSubirRockola.Click += delegate { MoverSeleccionCancion(-1); };
            btnBajarRockola.Click += delegate { MoverSeleccionCancion(1); };
            btnBuscarRockola.Click += delegate { busquedaMosaico.Focus(); };

            Panel panelCola = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 12, 25) };
            etiquetaCola.Text = "TEMAS EN ESPERA (0)"; etiquetaCola.Dock = DockStyle.Top; etiquetaCola.Height = 28;
            etiquetaCola.TextAlign = ContentAlignment.MiddleLeft; etiquetaCola.Padding = new Padding(7, 0, 0, 0); etiquetaCola.ForeColor = Color.Gold;
            cola.Dock = DockStyle.Fill; cola.View = View.Details; cola.FullRowSelect = true; cola.GridLines = false;
            cola.HideSelection = false; cola.BackColor = Color.FromArgb(4, 12, 25); cola.ForeColor = Color.White;
            cola.Columns.Clear(); cola.Columns.Add("SIGUIENTE CANCIÓN", 220); cola.Columns.Add("ARTISTA", 130);
            cola.DoubleClick += delegate { ReproducirElementoDeCola(); };
            FlowLayoutPanel accionesCola = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(2) };
            Button agregar = new Button { Text = "+ AGREGAR", Width = 92, Height = 32 };
            Button quitar = new Button { Text = "QUITAR", Width = 78, Height = 32 };
            Button subir = new Button { Text = "▲", Width = 42, Height = 32 };
            Button bajar = new Button { Text = "▼", Width = 42, Height = 32 };
            Button vaciar = new Button { Text = "VACIAR", Width = 78, Height = 32 };
            foreach (Button b in new Button[] { agregar, quitar, subir, bajar, vaciar }) { Estilizar(b); accionesCola.Controls.Add(b); }
            agregar.Click += delegate { AgregarSeleccionALaCola(); }; quitar.Click += delegate { QuitarDeLaCola(); }; subir.Click += delegate { MoverEnCola(-1); };
            bajar.Click += delegate { MoverEnCola(1); }; vaciar.Click += delegate { cola.Items.Clear(); ActualizarContadorCola(); };
            panelCola.Controls.Add(cola); panelCola.Controls.Add(accionesCola); panelCola.Controls.Add(etiquetaCola);
            panelDerecho.Controls.Add(panelCola); panelDerecho.Controls.Add(controlesRockola); panelDerecho.Controls.Add(botonesModo); panelDerecho.Controls.Add(panelPublicidad);
            division.Panel1.Controls.Add(lado); division.Panel2.Controls.Add(panelDerecho);
            Controls.Add(division); Controls.Add(inferior); Controls.Add(encabezado);
            Shown += delegate { try { division.SplitterDistance = Math.Max(650, (int)(ClientSize.Width * 0.70)); } catch { } };
        }

        private void ConstruirMosaico()
        {
            mosaico.SuspendLayout();
            mosaico.Controls.Clear();
            string texto = busquedaMosaico.Text == "Buscar canción o artista..." ? "" : busquedaMosaico.Text.Trim();
            System.Collections.Generic.Dictionary<string, ListViewItem> albumes = new System.Collections.Generic.Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);

            foreach (ListViewItem elemento in todosLosElementos)
            {
                string tipo = Convert.ToString(elemento.ToolTipText);
                if (filtroTipo != "Todos" && !tipo.Equals(filtroTipo, StringComparison.OrdinalIgnoreCase)) continue;
                if (texto.Length > 0 && elemento.Text.IndexOf(texto, StringComparison.OrdinalIgnoreCase) < 0 &&
                    elemento.SubItems[1].Text.IndexOf(texto, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string ruta = Convert.ToString(elemento.Tag);
                string carpeta = Path.GetDirectoryName(ruta);
                string nombreAlbum = String.IsNullOrWhiteSpace(carpeta) ? "MI COLECCIÓN" : new DirectoryInfo(carpeta).Name;
                if (!albumes.ContainsKey(nombreAlbum)) albumes.Add(nombreAlbum, elemento);
            }

            foreach (System.Collections.Generic.KeyValuePair<string, ListViewItem> album in albumes)
            {
                ListViewItem primerTema = album.Value;
                string carpeta = Path.GetDirectoryName(Convert.ToString(primerTema.Tag));
                int cantidad = 0;
                foreach (ListViewItem tema in todosLosElementos)
                {
                    if (String.Equals(Path.GetDirectoryName(Convert.ToString(tema.Tag)), carpeta, StringComparison.OrdinalIgnoreCase)) cantidad++;
                }

                Panel tarjeta = new Panel { Width = 226, Height = 202, Margin = new Padding(7, 3, 7, 3), BackColor = Color.FromArgb(0, 62, 125), Cursor = Cursors.Hand, Tag = primerTema };
                tarjeta.Paint += delegate(object sender, PaintEventArgs e)
                {
                    Panel p = (Panel)sender;
                    using (Pen borde = new Pen(Color.FromArgb(0, 220, 245), 3F)) e.Graphics.DrawRectangle(borde, 1, 1, p.Width - 3, p.Height - 3);
                };
                PictureBox portada = new PictureBox { Dock = DockStyle.Top, Height = 145, BackColor = Color.FromArgb(13, 32, 75), SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand };
                Image imagen = BuscarPortada(carpeta);
                if (imagen != null) portada.Image = imagen;
                else
                {
                    portada.Paint += delegate(object sender, PaintEventArgs e)
                    {
                        Rectangle r = ((PictureBox)sender).ClientRectangle;
                        using (System.Drawing.Drawing2D.LinearGradientBrush grad = new System.Drawing.Drawing2D.LinearGradientBrush(r, Color.FromArgb(18, 38, 105), Color.FromArgb(135, 25, 115), 35F)) e.Graphics.FillRectangle(grad, r);
                        using (Font f = new Font("Segoe UI Symbol", 52F, FontStyle.Bold))
                        using (SolidBrush b = new SolidBrush(Color.White)) e.Graphics.DrawString("♫", f, b, 68, 28);
                    };
                }
                Label nombre = new Label { Text = album.Key, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI", 11F, FontStyle.Bold), BackColor = Color.FromArgb(0, 95, 175), AutoEllipsis = true, Cursor = Cursors.Hand };
                Label total = new Label { Text = cantidad + " TEMAS", Dock = DockStyle.Bottom, Height = 23, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.FromArgb(0, 75, 145), Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
                tarjeta.Controls.Add(nombre); tarjeta.Controls.Add(total); tarjeta.Controls.Add(portada);
                EventHandler elegir = delegate { MostrarCancionesDeAlbum(carpeta); };
                tarjeta.Click += elegir; portada.Click += elegir; nombre.Click += elegir; total.Click += elegir;
                mosaico.Controls.Add(tarjeta);
            }
            mosaico.ResumeLayout();
        }

        private Image BuscarPortada(string carpeta)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(carpeta) || !Directory.Exists(carpeta)) return null;
                string[] nombres = new string[] { "cover.jpg", "cover.png", "folder.jpg", "folder.png", "front.jpg", "front.png", "caratula.jpg", "caratula.png", "album.jpg", "album.png" };
                foreach (string nombre in nombres)
                {
                    string archivo = Path.Combine(carpeta, nombre);
                    if (File.Exists(archivo))
                    {
                        using (FileStream fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (Image original = Image.FromStream(fs)) return new Bitmap(original);
                    }
                }
                foreach (string archivo in Directory.GetFiles(carpeta))
                {
                    string ext = Path.GetExtension(archivo).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                    {
                        using (FileStream fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (Image original = Image.FromStream(fs)) return new Bitmap(original);
                    }
                }
            }
            catch { }
            return null;
        }

        private void SeleccionarElemento(ListViewItem elemento)
        {
            if (elemento == null) return;
            MostrarCancionesDeAlbum(Path.GetDirectoryName(Convert.ToString(elemento.Tag)));
        }

        private void MostrarCancionesDeAlbum(string carpeta)
        {
            biblioteca.BeginUpdate();
            biblioteca.Items.Clear();
            foreach (ListViewItem original in todosLosElementos)
            {
                if (String.Equals(Path.GetDirectoryName(Convert.ToString(original.Tag)), carpeta, StringComparison.OrdinalIgnoreCase))
                {
                    ListViewItem copia = (ListViewItem)original.Clone();
                    biblioteca.Items.Add(copia);
                }
            }
            biblioteca.EndUpdate();
            if (biblioteca.Items.Count > 0)
            {
                biblioteca.Items[0].Selected = true;
                biblioteca.Items[0].Focused = true;
                biblioteca.Items[0].EnsureVisible();
            }
        }

        private static void Estilizar(Button boton)
        {
            boton.FlatStyle = FlatStyle.Flat; boton.FlatAppearance.BorderSize = 0;
            boton.BackColor = Color.FromArgb(30, 105, 170); boton.ForeColor = Color.White;
            boton.Font = new Font("Segoe UI", 9F, FontStyle.Bold); boton.Cursor = Cursors.Hand;
        }

        private void InicializarMotor()
        {
            try
            {
                string carpetaVlc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VLC", Environment.Is64BitProcess ? "x64" : "x86");
                if (!File.Exists(Path.Combine(carpetaVlc, "libvlc.dll")))
                    throw new FileNotFoundException("No se encontró el motor VLC de " + (Environment.Is64BitProcess ? "64" : "32") + " bits. Revisa que la carpeta VLC esté junto al programa.", carpetaVlc);
                Core.Initialize(carpetaVlc);
                motor = new LibVLC("--no-video-title-show", "--quiet");
                reproductor = new MediaPlayer(motor);
                pantalla.MediaPlayer = reproductor;
                reproductor.EndReached += delegate
                {
                    if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)ReproducirSiguiente);
                };
                reproductor.EncounteredError += delegate
                {
                    if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { tituloActual.Text = "No se pudo reproducir este archivo. Revisa su formato o ubicación."; });
                };
                reproductor.Volume = volumen.Value;
                reloj.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo iniciar el reproductor multimedia.\n\n" + ex.Message, "Motor multimedia", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarBiblioteca()
        {
            biblioteca.Items.Clear();
            todosLosElementos.Clear();
            try
            {
                using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
                using (SQLiteCommand comando = new SQLiteCommand(
                    "SELECT c.Titulo, IFNULL(a.Nombre,'Sin artista'), c.RutaArchivo, c.Tipo FROM Canciones c LEFT JOIN Artistas a ON a.Id=c.ArtistaId WHERE c.Activa=1 ORDER BY a.Nombre,c.Titulo;", conexion))
                {
                    conexion.Open();
                    using (SQLiteDataReader lector = comando.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            ListViewItem elemento = new ListViewItem(Convert.ToString(lector[0]));
                            elemento.SubItems.Add(Convert.ToString(lector[1]));
                            elemento.Tag = Convert.ToString(lector[2]);
                            elemento.ToolTipText = Convert.ToString(lector[3]);
                            biblioteca.Items.Add(elemento);
                            todosLosElementos.Add(elemento);
                        }
                    }
                }
                tituloActual.Text = "Biblioteca cargada: " + biblioteca.Items.Count + " canciones y videos";
                ConstruirMosaico();
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo cargar la biblioteca.\n" + ex.Message, "Biblioteca", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void MoverSeleccionCancion(int desplazamiento)
        {
            if (biblioteca.Items.Count == 0) return;
            int indice = biblioteca.SelectedIndices.Count > 0 ? biblioteca.SelectedIndices[0] : 0;
            indice = Math.Max(0, Math.Min(biblioteca.Items.Count - 1, indice + desplazamiento));
            LimpiarSeleccionBiblioteca();
            biblioteca.Items[indice].Selected = true;
            biblioteca.Items[indice].Focused = true;
            biblioteca.Items[indice].EnsureVisible();
        }

        private void ReproducirSeleccion()
        {
            if (biblioteca.SelectedIndices.Count == 0) return;
            ReproducirIndice(biblioteca.SelectedIndices[0]);
        }

        private void ReproducirIndice(int indice)
        {
            if (indice < 0 || indice >= biblioteca.Items.Count) return;
            ListViewItem elemento = biblioteca.Items[indice];
            ReproducirArchivo(Convert.ToString(elemento.Tag), elemento.Text, elemento.SubItems[1].Text);
            if (biblioteca.Items[indice].Selected)
            {
                biblioteca.Items[indice].EnsureVisible();
            }
            else
            {
                LimpiarSeleccionBiblioteca();
                biblioteca.Items[indice].Selected = true;
                biblioteca.Items[indice].EnsureVisible();
            }
        }

        private void ReproducirArchivo(string ruta, string titulo, string artista)
        {
            if (reproductor == null) { MessageBox.Show(this, "El motor multimedia no está disponible. Revisa la instalación.", "Reproductor"); return; }
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) { MessageBox.Show(this, "El archivo ya no está disponible:\n" + ruta, "Archivo no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                reproductor.Stop();
                if (medioActual != null) medioActual.Dispose();
                medioActual = new Media(motor, new Uri(Path.GetFullPath(ruta)));
                if (!reproductor.Play(medioActual)) throw new InvalidOperationException("VLC no pudo iniciar la reproducción.");
                tituloActual.Text = titulo + "  —  " + artista;
                reproducir.Text = "Ⅱ PAUSAR";
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo reproducir el archivo.\n" + ex.Message, "Error de reproducción", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void AgregarSeleccionALaCola()
        {
            if (biblioteca.SelectedIndices.Count == 0)
            {
                MessageBox.Show(this, "Primero selecciona una canción de la lista. Haz doble clic o usa + AGREGAR.", "Cola de reproducción", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ListViewItem origen = biblioteca.SelectedItems[0];
            ListViewItem nuevo = new ListViewItem(origen.Text);
            nuevo.SubItems.Add(origen.SubItems[1].Text);
            nuevo.Tag = Convert.ToString(origen.Tag);
            cola.Items.Add(nuevo);
            ActualizarContadorCola();
        }

        private void ReproducirElementoDeCola()
        {
            if (cola.SelectedIndices.Count == 0) return;
            int indice = cola.SelectedIndices[0];
            ListViewItem elemento = cola.Items[indice];
            string ruta = Convert.ToString(elemento.Tag);
            string titulo = elemento.Text;
            string artista = elemento.SubItems[1].Text;
            cola.Items.RemoveAt(indice);
            ActualizarContadorCola();
            ReproducirArchivo(ruta, titulo, artista);
            SeleccionarEnBiblioteca(ruta);
        }

        private void QuitarDeLaCola()
        {
            if (cola.SelectedIndices.Count == 0) return;
            cola.Items.RemoveAt(cola.SelectedIndices[0]);
            ActualizarContadorCola();
        }

        private void MoverEnCola(int desplazamiento)
        {
            if (cola.SelectedIndices.Count == 0) return;
            int indice = cola.SelectedIndices[0];
            int destino = indice + desplazamiento;
            if (destino < 0 || destino >= cola.Items.Count) return;
            ListViewItem elemento = (ListViewItem)cola.Items[indice].Clone();
            cola.Items.RemoveAt(indice);
            cola.Items.Insert(destino, elemento);
            elemento.Selected = true;
            elemento.EnsureVisible();
            ActualizarContadorCola();
        }

        private void ActualizarContadorCola()
        {
            etiquetaCola.Text = "COLA DE REPRODUCCIÓN (" + cola.Items.Count + ")";
        }

        private void LimpiarSeleccionBiblioteca()
        {
            foreach (ListViewItem elemento in biblioteca.Items)
                elemento.Selected = false;
        }

        private void SeleccionarEnBiblioteca(string ruta)
        {
            LimpiarSeleccionBiblioteca();
            for (int i = 0; i < biblioteca.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(biblioteca.Items[i].Tag), ruta, StringComparison.OrdinalIgnoreCase))
                {
                    biblioteca.Items[i].Selected = true;
                    biblioteca.Items[i].EnsureVisible();
                    return;
                }
            }
        }

        private void ReproducirAnterior()
        {
            if (biblioteca.SelectedIndices.Count == 0) return;
            int indice = biblioteca.SelectedIndices[0] - 1;
            if (indice < 0) indice = biblioteca.Items.Count - 1;
            ReproducirIndice(indice);
        }

        private void ReproducirSiguiente()
        {
            if (cola.Items.Count > 0)
            {
                ListViewItem elemento = cola.Items[0];
                string ruta = Convert.ToString(elemento.Tag);
                string titulo = elemento.Text;
                string artista = elemento.SubItems[1].Text;
                cola.Items.RemoveAt(0);
                ActualizarContadorCola();
                ReproducirArchivo(ruta, titulo, artista);
                SeleccionarEnBiblioteca(ruta);
                return;
            }

            if (biblioteca.Items.Count == 0) return;
            int indice = biblioteca.SelectedIndices.Count > 0 ? biblioteca.SelectedIndices[0] + 1 : 0;
            if (indice >= biblioteca.Items.Count) indice = 0;
            ReproducirIndice(indice);
        }

        private void Reloj_Tick(object sender, EventArgs e)
        {
            if (reproductor == null || !reproductor.IsPlaying) return;
            if (reproductor.Length > 0)
            {
                moviendoProgreso = true;
                progreso.Value = Math.Max(0, Math.Min(1000, (int)(reproductor.Position * 1000)));
                moviendoProgreso = false;
                tiempo.Text = FormatearTiempo(reproductor.Time) + " / " + FormatearTiempo(reproductor.Length);
            }
        }

        private static string FormatearTiempo(long milisegundos)
        {
            long segundosTotales = Math.Max(0L, milisegundos) / 1000L;
            long horas = segundosTotales / 3600L;
            long minutos = (segundosTotales % 3600L) / 60L;
            long segundos = segundosTotales % 60L;
            if (horas > 0)
                return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", horas, minutos, segundos);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutos, segundos);
        }

        private void LiberarMotor()
        {
            reloj.Stop();
            if (reproductor != null) { reproductor.Stop(); reproductor.Dispose(); reproductor = null; }
            if (medioActual != null) { medioActual.Dispose(); medioActual = null; }
            if (motor != null) { motor.Dispose(); motor = null; }
        }
    }
}