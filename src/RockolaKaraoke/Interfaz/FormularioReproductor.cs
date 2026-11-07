using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private string textoLetrero = "QUIERE CANTAR QUIERES ESCUCHA MUSICA";
        private int velocidadLetrero = 180;
        private Color colorLetrero = Color.FromArgb(255, 35, 35);
        private string textoSinTemas = "NO HAY TEMAS EN ESPERA";
        private int velocidadInferior = 160;
        private Color colorInferior = Color.Yellow;
        private Color fondoInferior = Color.FromArgb(220, 0, 0);
        private LetreroLed letreroLed;
        private LetreroLed letreroInferior;
        private Label tituloPortadas;
        private Panel panelCola;
        private Panel barraAbc;
        private FlowLayoutPanel flujoAbc;
        private FlowLayoutPanel filtros;
        private FlowLayoutPanel listaEstilos;
        private const string TextoBusqueda = "Buscar canción o artista...";
        private readonly List<Button> botonesAbc = new List<Button>();
        private int indiceAbc;
        private readonly List<KeyValuePair<Button, string>> botonesTipo = new List<KeyValuePair<Button, string>>();
        private readonly List<string> nombresGenero = new List<string>();
        private readonly List<Panel> tarjetas = new List<Panel>();
        private readonly List<string> carpetasTarjetas = new List<string>();
        private int indicePortada;
        private Dictionary<string, string> aliasCarpetas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // --- VARIABLES PARA LA PORTADA Y SKINS ---
        private Panel panelPortadaPrincipal;
        private PictureBox pictureBoxPortada;
        private Label lblTituloPortada;
        private Label lblArtistaPortada;
        private Label lblDuracionPortada;
        private string rutaCajaVacia = "PortadaVacia.png";
        private string rutaLeonDefecto = "LeonDefault.jpg";
        private Timer temporizadorInactividad;

        private string ArchivoLetrero { get { return Path.Combine(Application.UserAppDataPath, "letrero-led.txt"); } }

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
            Load += delegate { InicializarMotor(); CargarBiblioteca(); ActiveControl = biblioteca; };
            FormClosing += delegate { LiberarMotor(); };
            reloj.Interval = 500;
            reloj.Tick += Reloj_Tick;
            CargarConfiguracionLetrero();
            AplicarConfiguracionLetreros();

            // Aplicar el skin guardado al iniciar
            ConfiguracionSkin.Aplicar(this);
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
            Panel encabezado = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.FromArgb(2, 5, 12), Padding = new Padding(8, 5, 8, 5) };
            letreroLed = new LetreroLed { Dock = DockStyle.Fill, ForeColor = colorLetrero, BackColor = Color.FromArgb(5, 5, 8), Font = new Font("Consolas", 24F, FontStyle.Bold) };
            letreroLed.Texto = textoLetrero;
            encabezado.Controls.Add(letreroLed);

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
            
            detener.Click += delegate 
            { 
                if (reproductor != null) reproductor.Stop(); 
                reproducir.Text = "▶ REPRODUCIR";
                panelPortadaPrincipal.Visible = false;
                if (temporizadorInactividad != null) temporizadorInactividad.Stop();
                ActualizarLetreroInferior(); 
            };

            volumen.Scroll += delegate { if (reproductor != null) reproductor.Volume = volumen.Value; };
            inferior.Controls.Add(controles); inferior.Controls.Add(tiempo); inferior.Controls.Add(progreso); inferior.Controls.Add(tituloActual);

            TableLayoutPanel centro = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(1, 5, 18), ColumnCount = 1, RowCount = 2, Padding = new Padding(5) };
            centro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            centro.RowStyles.Add(new RowStyle(SizeType.Percent, 43F));
            centro.RowStyles.Add(new RowStyle(SizeType.Percent, 57F));
            Panel marcoSuperior = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(2, 8, 24), Padding = new Padding(5), Margin = new Padding(3) };
            Panel filaInferior = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(1, 5, 18), Margin = new Padding(3) };
            TableLayoutPanel distribucionInferior = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.FromArgb(1, 5, 18) };
            distribucionInferior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
            distribucionInferior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            Panel lado = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 12, 30), Padding = new Padding(5) };
            AplicarMarcoNeon(marcoSuperior);
            AplicarMarcoNeon(lado);

            mosaico.Dock = DockStyle.Fill; mosaico.AutoScroll = true; mosaico.WrapContents = false;
            mosaico.FlowDirection = FlowDirection.LeftToRight; mosaico.Padding = new Padding(8, 7, 8, 5);
            mosaico.BackColor = Color.FromArgb(3, 10, 27);

            filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 5, 8, 3), WrapContents = false, BackColor = Color.FromArgb(0, 185, 205) };
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
            biblioteca.Columns.Clear(); biblioteca.Columns.Add("[ÁLBUM SELECCIONADO]  •  1 DE 15", 620); biblioteca.Columns.Add("ARTISTA", 260);
            biblioteca.OwnerDraw = true;
            biblioteca.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e) { e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(0, 175, 205)), e.Bounds); TextRenderer.DrawText(e.Graphics, e.Header.Text, new Font("Segoe UI", 10F, FontStyle.Bold), e.Bounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left); };
            biblioteca.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { e.DrawDefault = true; };
            biblioteca.DoubleClick += delegate { AgregarSeleccionALaCola(); };
            biblioteca.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
            {
                Color basico = e.ItemIndex % 2 == 0 ? Color.FromArgb(5, 15, 30) : Color.FromArgb(10, 22, 39);
                bool marcada = e.Item.Selected;
                Color tinta = marcada ? Color.Black : Color.White;
                Rectangle area = e.Bounds;
                int sangria = e.ColumnIndex == 0 ? 44 : 0;
                using (SolidBrush fondoBase = new SolidBrush(basico)) { e.Graphics.FillRectangle(fondoBase, area); }
                if (marcada)
                {
                    using (SolidBrush barraAmarilla = new SolidBrush(Color.FromArgb(245, 195, 0))) { e.Graphics.FillRectangle(barraAmarilla, area.X + sangria, area.Y, area.Width - sangria, area.Height); }
                }
                if (e.ColumnIndex == 0)
                {
                    int tamanoMarca = marcada ? 26 : 16;
                    Rectangle marca = new Rectangle(area.X + 8 + (26 - tamanoMarca) / 2, area.Y + (area.Height - tamanoMarca) / 2, tamanoMarca, tamanoMarca);
                    if (marcada)
                    {
                        using (SolidBrush punto = new SolidBrush(Color.FromArgb(245, 195, 0))) { e.Graphics.FillEllipse(punto, marca); }
                    }
                    else
                    {
                        using (SolidBrush cuadro = new SolidBrush(Color.Red)) { e.Graphics.FillRectangle(cuadro, marca); }
                    }
                }
                Rectangle zonaTexto = new Rectangle(area.X + sangria, area.Y, area.Width - sangria, area.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, biblioteca.Font, zonaTexto, tinta, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            biblioteca.SelectedIndexChanged += delegate { ActualizarEncabezadoLista(); };

            lado.Controls.Add(biblioteca); lado.Controls.Add(filtros); lado.Controls.Add(busquedaMosaico);
            
            TableLayoutPanel zonaSuperior = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.FromArgb(2, 8, 24), Padding = new Padding(4) };
            zonaSuperior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78F));
            zonaSuperior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            Panel cuadroPortadas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(3, 10, 27), Padding = new Padding(4), Margin = new Padding(3) };
            Panel cuadroEstilos = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 15, 35), Padding = new Padding(5), Margin = new Padding(3) };
            AplicarMarcoNeon(cuadroPortadas);
            AplicarMarcoNeon(cuadroEstilos);
            tituloPortadas = new Label { Text = "MINIATURAS DE ÁLBUMES  •  7 DE 54  •  GÉNERO 2 DE 4", Dock = DockStyle.Top, Height = 31, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(0, 235, 245), Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
            FlowLayoutPanel navegacionPortadas = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(4, 2, 2, 2) };
            Button portadaAnterior = new Button { Text = "◀ PORTADA", Width = 58, Height = 32 };
            Button portadaSiguiente = new Button { Text = "▶", Width = 112, Height = 32 };
            foreach (Button b in new Button[] { portadaAnterior, portadaSiguiente }) { Estilizar(b); b.BackColor = Color.FromArgb(0, 105, 175); navegacionPortadas.Controls.Add(b); }
            portadaAnterior.Click += delegate { MoverPortada(-1); };
            portadaSiguiente.Click += delegate { MoverPortada(1); };
            cuadroPortadas.Controls.Add(mosaico);
            cuadroPortadas.Controls.Add(navegacionPortadas);
            cuadroPortadas.Controls.Add(tituloPortadas);
            Label tituloEstilos = new Label { Text = "CATEGORÍA", Dock = DockStyle.Top, Height = 38, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(0, 235, 245), Font = new Font("Segoe UI", 14F, FontStyle.Bold) };
            listaEstilos = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(3) };
            cuadroEstilos.Controls.Add(listaEstilos);
            cuadroEstilos.Controls.Add(tituloEstilos);
            zonaSuperior.Controls.Add(cuadroPortadas, 0, 0);
            zonaSuperior.Controls.Add(cuadroEstilos, 1, 0);
            marcoSuperior.Controls.Add(zonaSuperior);
            distribucionInferior.Controls.Add(lado, 0, 0);
            Panel panelDerecho = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(2, 7, 17), Padding = new Padding(7) };
            Panel panelPublicidad = new Panel { Dock = DockStyle.Top, Height = 245, BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle };
            pantalla.Dock = DockStyle.Fill; pantalla.BackColor = Color.Black;
            Label publicidad = new Label { Text = "AGREGUE PUBLICIDAD ACA", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gold, Font = new Font("Segoe UI", 13F, FontStyle.Bold) };
            panelPublicidad.Controls.Add(pantalla); panelPublicidad.Controls.Add(publicidad);
            FlowLayoutPanel botonesModo = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(2, 6, 2, 2), WrapContents = false, BackColor = Color.FromArgb(0, 175, 200) };
            foreach (string modo in new string[] { "AUDIO", "VIDEO", "KARAOKE" })
            {
                Button b = new Button { Text = modo, Width = 96, Height = 38 };
                Estilizar(b); b.BackColor = modo == "KARAOKE" ? Color.FromArgb(245, 190, 0) : Color.FromArgb(0, 105, 175);
                botonesModo.Controls.Add(b);
                botonesTipo.Add(new KeyValuePair<Button, string>(b, modo == "AUDIO" ? "Musica" : modo == "VIDEO" ? "Video" : "Karaoke"));
                b.Click += delegate { filtroTipo = modo == "AUDIO" ? "Musica" : modo == "VIDEO" ? "Video" : "Karaoke"; ConstruirMosaico(); };
            }

            FlowLayoutPanel controlesRockola = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(5, 5, 2, 2), WrapContents = true, BackColor = Color.FromArgb(3, 10, 24) };
            Button btnAnteriorRockola = new Button { Text = "◀", Width = 48, Height = 38, Font = new Font("Segoe UI", 17F, FontStyle.Bold) };
            Button btnPlayRockola = new Button { Text = "▶", Width = 58, Height = 38, Font = new Font("Segoe UI", 17F, FontStyle.Bold) };
            Button btnSiguienteRockola = new Button { Text = "▶|", Width = 48, Height = 38, Font = new Font("Segoe UI", 15F, FontStyle.Bold) };
            Button btnSubirRockola = new Button { Text = "▲", Width = 48, Height = 38, Font = new Font("Segoe UI", 15F, FontStyle.Bold) };
            Button btnBajarRockola = new Button { Text = "▼", Width = 48, Height = 38, Font = new Font("Segoe UI", 15F, FontStyle.Bold) };
            Button btnBuscarRockola = new Button { Text = "ABC", Width = 62, Height = 38, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            Button btnAyudaRockola = new Button { Text = "Q\nAYUDA", Width = 55, Height = 38, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            Button btnFullRockola = new Button { Text = "FULL", Width = 55, Height = 38, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            foreach (Button b in new Button[] { btnAnteriorRockola, btnPlayRockola, btnSiguienteRockola, btnSubirRockola, btnBajarRockola, btnBuscarRockola, btnAyudaRockola, btnFullRockola })
            {
                Estilizar(b); b.BackColor = Color.FromArgb(0, 105, 175); b.FlatAppearance.BorderColor = Color.FromArgb(0, 220, 245);
                controlesRockola.Controls.Add(b);
            }
            btnAnteriorRockola.Click += delegate { ReproducirAnterior(); };
            btnPlayRockola.Click += delegate { reproducir.PerformClick(); btnPlayRockola.Text = reproductor != null && reproductor.State == VLCState.Playing ? "Ⅱ" : "▶"; };
            btnSiguienteRockola.Click += delegate { ReproducirSiguiente(); };
            btnSubirRockola.Click += delegate { MoverSeleccionCancion(-1); };
            btnBajarRockola.Click += delegate { MoverSeleccionCancion(1); };
            btnBuscarRockola.Click += delegate { AlternarAbc(); };
            btnAyudaRockola.Click += delegate { MessageBox.Show(this, "Selecciona un álbum y después una canción. Usa PLAY para reproducir y ABC para buscar.", "Ayuda - Rockola", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            btnFullRockola.Click += delegate { if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal; else WindowState = FormWindowState.Maximized; };

            panelCola = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 12, 25) };
            etiquetaCola.Text = "NO HAY TEMAS EN ESPERA"; etiquetaCola.Dock = DockStyle.Top; etiquetaCola.Height = 28;
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
            distribucionInferior.Controls.Add(lado, 0, 0);
            distribucionInferior.Controls.Add(panelDerecho, 1, 0);
            filaInferior.Controls.Add(distribucionInferior);
            centro.Controls.Add(marcoSuperior, 0, 0);
            centro.Controls.Add(filaInferior, 0, 1);

            // --- CREACIÓN DEL PANEL DE PORTADA (Fondo transparente para ver el Skin) ---
            panelPortadaPrincipal = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent, 
                Visible = false
            };

            TableLayoutPanel tablaPortada = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(30)
            };
            tablaPortada.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            tablaPortada.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

            pictureBoxPortada = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            Panel contenedorTextos = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(150, 0, 0, 0) };
            FlowLayoutPanel panelTextos = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(10)
            };
            lblTituloPortada = new Label { ForeColor = Color.White, Font = new Font("Segoe UI", 26F, FontStyle.Bold), AutoSize = true };
            lblArtistaPortada = new Label { ForeColor = Color.LightGray, Font = new Font("Segoe UI", 18F), AutoSize = true };
            lblDuracionPortada = new Label { ForeColor = Color.White, Font = new Font("Segoe UI", 14F), AutoSize = true };
            
            panelTextos.Controls.Add(lblTituloPortada);
            panelTextos.Controls.Add(lblArtistaPortada);
            panelTextos.Controls.Add(lblDuracionPortada);
            contenedorTextos.Controls.Add(panelTextos);

            tablaPortada.Controls.Add(pictureBoxPortada, 0, 0);
            tablaPortada.Controls.Add(contenedorTextos, 0, 1);
            panelPortadaPrincipal.Controls.Add(tablaPortada);
            // ------------------------------------------

            // --- BARRA DE ESTADO INFERIOR (SIN LOS TEXTOS DE VENTA Y KARAOKE) ---
            Panel barraEstado = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.FromArgb(0, 105, 145) };
            letreroInferior = new LetreroLed { Dock = DockStyle.Fill, ForeColor = colorInferior, BackColor = fondoInferior, Font = new Font("Arial Black", 22F, FontStyle.Bold) };
            letreroInferior.Texto = textoSinTemas;
            barraEstado.Controls.Add(letreroInferior);
            // ------------------------------------------------------------------

            CrearBarraAbc();
            
            Controls.Add(centro); 
            Controls.Add(panelPortadaPrincipal);
            Controls.Add(barraAbc); 
            Controls.Add(barraEstado); 
            Controls.Add(inferior); 
            Controls.Add(encabezado);
            
            Shown += delegate { mosaico.Height = Math.Max(150, marcoSuperior.ClientSize.Height - 80); };

            // --- CONFIGURACIÓN DEL TEMPORIZADOR DE INACTIVIDAD ---
            temporizadorInactividad = new Timer();
            temporizadorInactividad.Interval = 3000; // 3 segundos
            temporizadorInactividad.Tick += delegate 
            {
                if (reproductor != null && reproductor.IsPlaying)
                {
                    panelPortadaPrincipal.Visible = true;
                    panelPortadaPrincipal.BringToFront();
                }
                temporizadorInactividad.Stop();
            };
        }

        private static Color LeerColor(string html, Color porDefecto)
        {
            try { return ColorTranslator.FromHtml(html); }
            catch { return porDefecto; }
        }

        private void CargarConfiguracionLetrero()
        {
            try
            {
                if (!File.Exists(ArchivoLetrero)) return;
                string[] lineas = File.ReadAllLines(ArchivoLetrero);
                int velocidad;
                if (lineas.Length > 0 && !String.IsNullOrWhiteSpace(lineas[0])) textoLetrero = lineas[0];
                if (lineas.Length > 1 && Int32.TryParse(lineas[1], out velocidad)) velocidadLetrero = Math.Max(60, Math.Min(500, velocidad));
                if (lineas.Length > 2) colorLetrero = LeerColor(lineas[2], colorLetrero);
                if (lineas.Length > 3 && !String.IsNullOrWhiteSpace(lineas[3])) textoSinTemas = lineas[3];
                if (lineas.Length > 4 && Int32.TryParse(lineas[4], out velocidad)) velocidadInferior = Math.Max(60, Math.Min(500, velocidad));
                if (lineas.Length > 5) colorInferior = LeerColor(lineas[5], colorInferior);
                if (lineas.Length > 6) fondoInferior = LeerColor(lineas[6], fondoInferior);
            }
            catch { }
        }

        private void GuardarConfiguracionLetrero()
        {
            try
            {
                string carpeta = Path.GetDirectoryName(ArchivoLetrero);
                if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);
                File.WriteAllLines(ArchivoLetrero, new string[]
                {
                    textoLetrero,
                    velocidadLetrero.ToString(CultureInfo.InvariantCulture),
                    ColorTranslator.ToHtml(colorLetrero),
                    textoSinTemas,
                    velocidadInferior.ToString(CultureInfo.InvariantCulture),
                    ColorTranslator.ToHtml(colorInferior),
                    ColorTranslator.ToHtml(fondoInferior)
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo guardar el letrero: " + ex.Message, "Letrero LED", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AplicarConfiguracionLetreros()
        {
