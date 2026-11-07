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
        private string rutaActualReproduccion = "";
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
        private Label tituloEstilos;
        private const string TextoBusqueda = "Buscar canción o artista...";
        private readonly List<Button> botonesAbc = new List<Button>();
        private int indiceAbc;
        private readonly List<KeyValuePair<Button, string>> botonesTipo = new List<KeyValuePair<Button, string>>();
        private readonly List<string> nombresGenero = new List<string>();
        private readonly List<Panel> tarjetas = new List<Panel>();
        private readonly List<string> carpetasTarjetas = new List<string>();
        private int indicePortada;
        private int inicioPaginaPortadas;
        private const int PortadasPorPagina = 7;
        private Dictionary<string, string> aliasCarpetas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // --- VARIABLES PARA LA PORTADA Y SKINS ---
        private Panel panelPortadaPrincipal;
        private Control contenidoCentral;
        private PictureBox pictureBoxPortada;
        private Label lblTituloPortada;
        private Label lblArtistaPortada;
        private Label lblDuracionPortada;
        private string rutaCajaVacia = Path.Combine(Application.StartupPath, "PortadaVacia.png.png");
        private string rutaLeonDefecto = Path.Combine(Application.StartupPath, "LeonDefault.jpg.jpg");
        private Timer temporizadorInactividad;
        private Timer temporizadorAbecedario;
        private AccionTeclado? direccionFlechaPendiente;
        private bool abecedarioAbiertoPorEspera;

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

            // Capturar las teclas en el formulario antes que los controles hijos.
            // Así las acciones configuradas siguen funcionando aunque el foco esté
            // en un botón, la lista, el control de volumen o el cuadro de búsqueda.
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                Keys teclaBase = e.KeyCode;
                AccionTeclado? accion = ConfiguracionTeclado.Buscar(teclaBase);

                if (ActiveControl is TextBoxBase && !accion.HasValue)
                {
                    if (teclaBase == Keys.Escape || teclaBase == Keys.Down)
                    {
                        ActiveControl = biblioteca;
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    return;
                }

                if (teclaBase == Keys.F12)
                {
                    MostrarAdministracion();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }

                if (!accion.HasValue) return;

                NotificarActividadUsuario();
                if (barraAbc != null && barraAbc.Visible && ManejarTeclaAbc(accion.Value))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }

                if (ConfiguracionTeclado.MostrarAbecedarioAlMantenerFlecha &&
                    (accion.Value == AccionTeclado.Izquierda || accion.Value == AccionTeclado.Derecha))
                {
                    if (!direccionFlechaPendiente.HasValue)
                    {
                        direccionFlechaPendiente = accion.Value;
                        abecedarioAbiertoPorEspera = false;
                        temporizadorAbecedario.Interval = ConfiguracionTeclado.TiempoEsperaAbecedario * 1000;
                        temporizadorAbecedario.Start();
                    }
                }
                else
                {
                    EjecutarAccion(accion.Value);
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
            };

            temporizadorAbecedario = new Timer();
            temporizadorAbecedario.Interval = ConfiguracionTeclado.TiempoEsperaAbecedario * 1000;
            temporizadorAbecedario.Tick += delegate
            {
                temporizadorAbecedario.Stop();
                if (direccionFlechaPendiente.HasValue && ConfiguracionTeclado.MostrarAbecedarioAlMantenerFlecha)
                {
                    abecedarioAbiertoPorEspera = true;
                    AlternarAbc();
                }
            };
            // KeyPreview permite recibir la liberación aunque el foco esté en la lista
            // o en otro control. Así las flechas izquierda/derecha siempre se ejecutan.
            KeyUp += delegate(object sender, KeyEventArgs e)
            {
                if (!direccionFlechaPendiente.HasValue) return;
                Keys teclaLiberada = e.KeyCode;
                AccionTeclado accionPendiente = direccionFlechaPendiente.Value;
                Keys teclaEsperada = ConfiguracionTeclado.Obtener(accionPendiente) & Keys.KeyCode;
                if (teclaLiberada != teclaEsperada) return;
                direccionFlechaPendiente = null;
                temporizadorAbecedario.Stop();
                if (!abecedarioAbiertoPorEspera)
                    MoverPortada(accionPendiente == AccionTeclado.Izquierda ? -1 : 1);
                abecedarioAbiertoPorEspera = false;
            };
            Load += delegate
            {
                InicializarMotor();
                CargarBiblioteca();
                MostrarPortadaEnEspera();
                ActiveControl = biblioteca;
            };
            FormClosing += delegate { LiberarMotor(); };
            Activated += delegate { AplicarVisibilidadGeneros(); };
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
                rutaActualReproduccion = "";
                reproducir.Text = "▶ REPRODUCIR";
                if (temporizadorInactividad != null) temporizadorInactividad.Stop();
                MostrarPortadaEnEspera();
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

            mosaico.Dock = DockStyle.Fill; mosaico.AutoScroll = false; mosaico.WrapContents = false;
            mosaico.Resize += delegate { ActualizarPaginaPortadas(); };
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
            biblioteca.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            ImageList altoFilas = new ImageList { ImageSize = new Size(1, 40), ColorDepth = ColorDepth.Depth32Bit };
            altoFilas.Images.Add(new Bitmap(1, 40));
            biblioteca.SmallImageList = altoFilas;
            biblioteca.Columns.Clear(); biblioteca.Columns.Add("ÁLBUM SELECCIONADO  •  CANCIÓN 1 DE 15", 620); biblioteca.Columns.Add("ARTISTA", 260);
            biblioteca.OwnerDraw = true;
            biblioteca.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e) { e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(0, 175, 205)), e.Bounds); TextRenderer.DrawText(e.Graphics, e.Header.Text, new Font("Segoe UI", 15F, FontStyle.Bold), e.Bounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis); };
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
            biblioteca.Resize += delegate { AjustarColumnasBiblioteca(); };

            lado.Controls.Add(biblioteca); lado.Controls.Add(filtros); lado.Controls.Add(busquedaMosaico);
            
            TableLayoutPanel zonaSuperior = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.FromArgb(2, 8, 24), Padding = new Padding(4) };
            zonaSuperior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78F));
            zonaSuperior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            Panel cuadroPortadas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(3, 10, 27), Padding = new Padding(4), Margin = new Padding(3) };
            Panel cuadroEstilos = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 15, 35), Padding = new Padding(5), Margin = new Padding(3) };
            AplicarMarcoNeon(cuadroPortadas);
            AplicarMarcoNeon(cuadroEstilos);
            tituloPortadas = new Label { Text = "PORTADAS 1–7", Dock = DockStyle.Top, Height = 38, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(0, 235, 245), Font = new Font("Segoe UI", 14F, FontStyle.Bold) };
            FlowLayoutPanel navegacionPortadas = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(4, 3, 2, 2) };
            Button portadaAnterior = new Button { Text = "◀ ANTERIOR 7", Width = 145, Height = 36 };
            Button portadaSiguiente = new Button { Text = "SIGUIENTE 7 ▶", Width = 155, Height = 36 };
            foreach (Button b in new Button[] { portadaAnterior, portadaSiguiente }) { Estilizar(b); b.Font = new Font("Segoe UI", 10F, FontStyle.Bold); b.BackColor = Color.FromArgb(0, 105, 175); navegacionPortadas.Controls.Add(b); }
            portadaAnterior.Click += delegate { MoverPaginaPortadas(-1); };
            portadaSiguiente.Click += delegate { MoverPaginaPortadas(1); };
            cuadroPortadas.Controls.Add(mosaico);
            cuadroPortadas.Controls.Add(navegacionPortadas);
            cuadroPortadas.Controls.Add(tituloPortadas);
            tituloEstilos = new Label { Text = "CATEGORÍA", Dock = DockStyle.Top, Height = 38, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(0, 235, 245), Font = new Font("Segoe UI", 14F, FontStyle.Bold) };
            listaEstilos = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(3) };
            cuadroEstilos.Controls.Add(listaEstilos);
            cuadroEstilos.Controls.Add(tituloEstilos);
            AplicarVisibilidadGeneros();
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
            etiquetaCola.Text = "LISTA DE REPRODUCCIÓN • VACÍA"; etiquetaCola.Dock = DockStyle.Top; etiquetaCola.Height = 38;
            etiquetaCola.TextAlign = ContentAlignment.MiddleLeft; etiquetaCola.Padding = new Padding(9, 0, 0, 0); etiquetaCola.ForeColor = Color.Gold;
            etiquetaCola.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            cola.Dock = DockStyle.Fill; cola.View = View.Details; cola.FullRowSelect = true; cola.GridLines = false;
            cola.HideSelection = false; cola.BackColor = Color.FromArgb(4, 12, 25); cola.ForeColor = Color.White;
            cola.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            cola.Columns.Clear(); cola.Columns.Add("TEMA EN LA LISTA", 220); cola.Columns.Add("ARTISTA", 130);
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

            // --- PORTADA DE ESPERA: CAJA DE CD ABIERTA SOBRE FONDO ROJO ---
            panelPortadaPrincipal = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(190, 0, 0),
                Visible = false
            };

            pictureBoxPortada = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top
            };
            lblTituloPortada = new Label
            {
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            lblArtistaPortada = new Label
            {
                ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 11F),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            lblDuracionPortada = new Label
            {
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            // El propio panel es el fondo rojo de toda el área central.
            // No usar otro Panel Dock=Fill: dos controles Fill superpuestos pueden
            // repartirse el espacio durante el layout de WinForms.
            panelPortadaPrincipal.Controls.Add(pictureBoxPortada);
            panelPortadaPrincipal.Controls.Add(lblTituloPortada);
            panelPortadaPrincipal.Controls.Add(lblArtistaPortada);
            panelPortadaPrincipal.Controls.Add(lblDuracionPortada);

            pictureBoxPortada.BringToFront();
            lblTituloPortada.BringToFront();
            lblArtistaPortada.BringToFront();
            lblDuracionPortada.BringToFront();

            panelPortadaPrincipal.Resize += delegate { AjustarComposicionPortada(); };
            // ------------------------------------------------------------

            // --- BARRA DE ESTADO INFERIOR (SIN LOS TEXTOS DE VENTA Y KARAOKE) ---
            Panel barraEstado = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.FromArgb(0, 105, 145) };
            letreroInferior = new LetreroLed { Dock = DockStyle.Fill, ForeColor = colorInferior, BackColor = fondoInferior, Font = new Font("Arial Black", 22F, FontStyle.Bold) };
            letreroInferior.Texto = textoSinTemas;
            barraEstado.Controls.Add(letreroInferior);
            // ------------------------------------------------------------------

            CrearBarraAbc();
            
            // Contenedor independiente para el área central: la portada no debe
            // agregarse al TableLayoutPanel porque este la asigna a una celda.
            Panel contenedorCentral = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(190, 0, 0),
                Margin = Padding.Empty
            };
            // Alternar dos vistas hermanas: nunca superponer dos controles Fill.
            // En espera solo está visible la portada roja; al reproducir, solo el reproductor.
            contenidoCentral = centro;
            centro.Dock = DockStyle.Fill;
            centro.Visible = false;
            contenedorCentral.Controls.Add(centro);

            panelPortadaPrincipal.Dock = DockStyle.Fill;
            panelPortadaPrincipal.Margin = Padding.Empty;
            panelPortadaPrincipal.BackColor = Color.FromArgb(190, 0, 0);
            panelPortadaPrincipal.Visible = true;
            contenedorCentral.Controls.Add(panelPortadaPrincipal);
            panelPortadaPrincipal.BringToFront();

            Controls.Add(contenedorCentral);
            Controls.Add(barraAbc);
            Controls.Add(barraEstado);
            Controls.Add(inferior);
            Controls.Add(encabezado);
            Controls.SetChildIndex(contenedorCentral, Controls.Count - 1);
            Shown += delegate
            {
                mosaico.Height = Math.Max(150, marcoSuperior.ClientSize.Height - 80);
                AjustarComposicionPortada();
                panelPortadaPrincipal.BringToFront();
            };

            // --- CONFIGURACIÓN DEL TEMPORIZADOR DE INACTIVIDAD ---
            temporizadorInactividad = new Timer();
            temporizadorInactividad.Interval = 3000; // 3 segundos sin actividad
            temporizadorInactividad.Tick += delegate
            {
                temporizadorInactividad.Stop();
                // Durante la reproducción, volver a mostrar la portada roja de la caja del CD.
                if (reproductor != null && reproductor.IsPlaying &&
                    !String.IsNullOrWhiteSpace(rutaActualReproduccion))
                    MostrarPortadaCancion();
            };

            // Detectar movimiento del mouse en toda la interfaz, no solo sobre el formulario.
            RegistrarMovimientoMouse(this);
        }

        private void RegistrarMovimientoMouse(Control control)
        {
            if (control == null) return;
            control.MouseMove += delegate { NotificarActividadUsuario(); };
            foreach (Control hijo in control.Controls)
                RegistrarMovimientoMouse(hijo);
        }

        private void NotificarActividadUsuario()
        {
            if (reproductor == null || !reproductor.IsPlaying) return;
            if (panelPortadaPrincipal.Visible)
                MostrarContenidoReproductor();
            temporizadorInactividad.Stop();
            temporizadorInactividad.Start();
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
            if (letreroLed != null)
            {
                letreroLed.Texto = textoLetrero;
                letreroLed.ForeColor = colorLetrero;
                letreroLed.PixelesPorSegundo = 18000F / velocidadLetrero;
            }
            if (letreroInferior != null)
            {
                letreroInferior.ForeColor = colorInferior;
                letreroInferior.BackColor = fondoInferior;
                letreroInferior.PixelesPorSegundo = 18000F / velocidadInferior;
            }
            ActualizarLetreroInferior();
        }

        private void ActualizarLetreroInferior()
        {
            if (letreroInferior == null) return;
            if (cola.Items.Count == 0 && (reproductor == null || !reproductor.IsPlaying)) 
            { 
                letreroInferior.Texto = textoSinTemas; 
                return; 
            }
            
            if (cola.Items.Count > 0)
            {
                List<string> partes = new List<string>();
                for (int i = 0; i < cola.Items.Count; i++)
                    partes.Add((i + 1) + ". " + cola.Items[i].Text + " - " + cola.Items[i].SubItems[1].Text);
                letreroInferior.Texto = "EN ESPERA:  " + String.Join("   •   ", partes.ToArray());
            }
        }

        private static void ElegirColor(IWin32Window dueno, Panel muestra)
        {
            using (ColorDialog selector = new ColorDialog { Color = muestra.BackColor, FullOpen = true })
            {
                if (selector.ShowDialog(dueno) == DialogResult.OK) muestra.BackColor = selector.Color;
            }
        }

        private void MostrarConfigLetreros()
        {
            using (Form menu = new Form())
            {
                menu.Text = "CONFIGURAR LETREROS LED - ROCKOLA KARAOKE";
                menu.StartPosition = FormStartPosition.CenterParent;
                menu.FormBorderStyle = FormBorderStyle.FixedDialog;
                menu.MaximizeBox = false;
                menu.MinimizeBox = false;
                menu.ClientSize = new Size(540, 500);
                menu.BackColor = Color.FromArgb(5, 14, 32);
                menu.ForeColor = Color.White;
                Font negrita = new Font("Segoe UI", 13F, FontStyle.Bold);
                Color cian = Color.FromArgb(0, 235, 245);

                Label h1 = new Label { Text = "LETRERO SUPERIOR", Left = 15, Top = 10, Width = 510, Height = 28, ForeColor = cian, Font = negrita };
                Label l1 = new Label { Text = "Texto que camina de izquierda a derecha:", Left = 15, Top = 42, Width = 510, Height = 22 };
                TextBox t1 = new TextBox { Left = 15, Top = 66, Width = 510, Text = textoLetrero, Font = new Font("Segoe UI", 11F) };
                Label l2 = new Label { Text = "Velocidad (ms por avance, menor = más rápido):", Left = 15, Top = 108, Width = 310, Height = 24 };
                NumericUpDown v1 = new NumericUpDown { Left = 330, Top = 105, Width = 100, Minimum = 60, Maximum = 500, Increment = 10, Value = velocidadLetrero };
                Label l3 = new Label { Text = "Color de las letras:", Left = 15, Top = 150, Width = 220, Height = 24 };
                Panel c1 = new Panel { Left = 250, Top = 146, Width = 48, Height = 28, BackColor = colorLetrero, BorderStyle = BorderStyle.FixedSingle };
                Button b1 = new Button { Text = "ELEGIR COLOR", Left = 310, Top = 142, Width = 200, Height = 34 };

                Label h2 = new Label { Text = "LETRERO INFERIOR", Left = 15, Top = 200, Width = 510, Height = 28, ForeColor = cian, Font = negrita };
                Label l4 = new Label { Text = "Texto cuando no hay temas en espera:", Left = 15, Top = 232, Width = 510, Height = 22 };
                TextBox t2 = new TextBox { Left = 15, Top = 256, Width = 510, Text = textoSinTemas, Font = new Font("Segoe UI", 11F) };
                Label l5 = new Label { Text = "Velocidad (ms por avance, menor = más rápido):", Left = 15, Top = 298, Width = 310, Height = 24 };
                NumericUpDown v2 = new NumericUpDown { Left = 330, Top = 295, Width = 100, Minimum = 60, Maximum = 500, Increment = 10, Value = velocidadInferior };
                Label l6 = new Label { Text = "Color de las letras:", Left = 15, Top = 340, Width = 220, Height = 24 };
                Panel c2 = new Panel { Left = 250, Top = 336, Width = 48, Height = 28, BackColor = colorInferior, BorderStyle = BorderStyle.FixedSingle };
                Button b2 = new Button { Text = "ELEGIR COLOR", Left = 310, Top = 332, Width = 200, Height = 34 };
                Label l7 = new Label { Text = "Color del fondo:", Left = 15, Top = 382, Width = 220, Height = 24 };
                Panel c3 = new Panel { Left = 250, Top = 378, Width = 48, Height = 28, BackColor = fondoInferior, BorderStyle = BorderStyle.FixedSingle };
                Button b3 = new Button { Text = "ELEGIR COLOR", Left = 310, Top = 374, Width = 200, Height = 34 };

                Button guardar = new Button { Text = "GUARDAR CAMBIOS", Left = 15, Top = 440, Width = 250, Height = 44 };
                Button cerrar = new Button { Text = "CERRAR", Left = 280, Top = 440, Width = 245, Height = 44 };
                foreach (Button b in new Button[] { b1, b2, b3, guardar, cerrar }) { Estilizar(b); b.BackColor = Color.FromArgb(0, 105, 175); }
                b1.Click += delegate { ElegirColor(menu, c1); };
                b2.Click += delegate { ElegirColor(menu, c2); };
                b3.Click += delegate { ElegirColor(menu, c3); };
                guardar.Click += delegate
                {
                    if (String.IsNullOrWhiteSpace(t1.Text) || String.IsNullOrWhiteSpace(t2.Text)) { MessageBox.Show(menu, "Escribe el texto de los dos letreros.", "Letreros LED"); return; }
                    textoLetrero = t1.Text.Trim();
                    velocidadLetrero = Decimal.ToInt32(v1.Value);
                    colorLetrero = c1.BackColor;
                    textoSinTemas = t2.Text.Trim();
                    velocidadInferior = Decimal.ToInt32(v2.Value);
                    colorInferior = c2.BackColor;
                    fondoInferior = c3.BackColor;
                    AplicarConfiguracionLetreros();
                    GuardarConfiguracionLetrero();
                    MessageBox.Show(menu, "Los letreros LED se actualizaron y quedaron guardados.", "Letreros LED", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                cerrar.Click += delegate { menu.Close(); };
                menu.CancelButton = cerrar;
                menu.Controls.Add(h1); menu.Controls.Add(l1); menu.Controls.Add(t1); menu.Controls.Add(l2); menu.Controls.Add(v1);
                menu.Controls.Add(l3); menu.Controls.Add(c1); menu.Controls.Add(b1);
                menu.Controls.Add(h2); menu.Controls.Add(l4); menu.Controls.Add(t2); menu.Controls.Add(l5); menu.Controls.Add(v2);
                menu.Controls.Add(l6); menu.Controls.Add(c2); menu.Controls.Add(b2); menu.Controls.Add(l7); menu.Controls.Add(c3); menu.Controls.Add(b3);
                menu.Controls.Add(guardar); menu.Controls.Add(cerrar);
                menu.ShowDialog(this);
            }
        }

        private void DesplazarPortadas(int direccion)
        {
            if (mosaico.Controls.Count == 0) return;
            int paso = Math.Max(220, mosaico.ClientSize.Width / 2);
            int maximo = Math.Max(0, mosaico.HorizontalScroll.Maximum - mosaico.HorizontalScroll.LargeChange + 1);
            int destino = Math.Max(0, Math.Min(maximo, mosaico.HorizontalScroll.Value + (direccion * paso)));
            mosaico.AutoScrollPosition = new Point(destino, 0);
        }

        private string NombreAlbum(string carpeta)
        {
            if (String.IsNullOrWhiteSpace(carpeta)) return "MI COLECCIÓN";
            string alias;
            if (aliasCarpetas.TryGetValue(carpeta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), out alias) && !String.IsNullOrWhiteSpace(alias)) return alias;
            return new DirectoryInfo(carpeta).Name;
        }

        private void ConstruirMosaico()
        {
            mosaico.SuspendLayout();
            mosaico.Controls.Clear();
            tarjetas.Clear();
            carpetasTarjetas.Clear();
            string texto = TextoBuscado().Trim();
            Dictionary<string, ListViewItem> albumes = new Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> conteo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<string> orden = new List<string>();

            foreach (ListViewItem elemento in todosLosElementos)
            {
                string carpetaTema = Path.GetDirectoryName(Convert.ToString(elemento.Tag)) ?? "";
                int actual;
                conteo.TryGetValue(carpetaTema, out actual);
                conteo[carpetaTema] = actual + 1;

                if (!String.Equals(filtroTipo, "Todos", StringComparison.OrdinalIgnoreCase))
                {
                    string comparar = String.IsNullOrEmpty(elemento.Name) ? Convert.ToString(elemento.ToolTipText) : elemento.Name;
                    if (!IgualNormal(comparar, filtroTipo)) continue;
                }
                if (texto.Length > 0 && elemento.Text.IndexOf(texto, StringComparison.OrdinalIgnoreCase) < 0 &&
                    elemento.SubItems[1].Text.IndexOf(texto, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!albumes.ContainsKey(carpetaTema)) { albumes.Add(carpetaTema, elemento); orden.Add(carpetaTema); }
            }
            orden.Sort(delegate(string a, string b) { return String.Compare(NombreAlbum(a), NombreAlbum(b), StringComparison.CurrentCultureIgnoreCase); });

            foreach (string carpeta in orden)
            {
                ListViewItem primerTema = albumes[carpeta];
                int cantidad;
                conteo.TryGetValue(carpeta, out cantidad);
                int posicion = tarjetas.Count;

                Panel tarjeta = new Panel { Width = 178, Height = 205, Margin = new Padding(9, 5, 9, 5), BackColor = Color.FromArgb(0, 62, 125), Cursor = Cursors.Hand, Tag = primerTema };
                tarjeta.Paint += delegate(object sender, PaintEventArgs e)
                {
                    Panel p = (Panel)sender;
                    bool elegida = tarjetas.IndexOf(p) == indicePortada;
                    using (Pen borde = new Pen(elegida ? Color.FromArgb(255, 215, 0) : Color.FromArgb(0, 220, 245), elegida ? 5F : 3F)) e.Graphics.DrawRectangle(borde, 1, 1, p.Width - 3, p.Height - 3);
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
                        using (Font f = new Font("Segoe UI Symbol", 48F, FontStyle.Bold))
                        using (SolidBrush b = new SolidBrush(Color.White)) e.Graphics.DrawString("♫", f, b, 58, 28);
                    };
                }
                Label nombre = new Label { Text = NombreAlbum(carpeta), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold), BackColor = Color.FromArgb(0, 95, 175), AutoEllipsis = true, Cursor = Cursors.Hand };
                Label total = new Label { Text = cantidad + " TEMAS", Dock = DockStyle.Bottom, Height = 25, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.FromArgb(0, 75, 145), Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand };
                tarjeta.Controls.Add(nombre); tarjeta.Controls.Add(total); tarjeta.Controls.Add(portada);
                EventHandler elegir = delegate { SeleccionarPortada(posicion); };
                tarjeta.Click += elegir; portada.Click += elegir; nombre.Click += elegir; total.Click += elegir;
                mosaico.Controls.Add(tarjeta);
                tarjetas.Add(tarjeta);
                carpetasTarjetas.Add(carpeta);
            }
            mosaico.ResumeLayout();
            ResaltarTipos();
            indicePortada = 0;
            inicioPaginaPortadas = 0;
            ActualizarPaginaPortadas();
            SeleccionarPortada(0);
        }

        private void ResaltarPortadas()
        {
            for (int i = 0; i < tarjetas.Count; i++)
            {
                bool elegida = i == indicePortada;
                tarjetas[i].BackColor = elegida ? Color.FromArgb(245, 190, 0) : Color.FromArgb(0, 62, 125);
                foreach (Control c in tarjetas[i].Controls)
                {
                    Label etiqueta = c as Label;
                    if (etiqueta == null) continue;
                    bool principal = etiqueta.Dock == DockStyle.Fill;
                    if (elegida) { etiqueta.BackColor = principal ? Color.FromArgb(245, 190, 0) : Color.FromArgb(215, 160, 0); etiqueta.ForeColor = Color.Black; }
                    else { etiqueta.BackColor = principal ? Color.FromArgb(0, 95, 175) : Color.FromArgb(0, 75, 145); etiqueta.ForeColor = Color.White; }
                }
                tarjetas[i].Invalidate();
            }
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
                if (String.Equals(Path.GetDirectoryName(Convert.ToString(original.Tag)) ?? "", carpeta ?? "", StringComparison.OrdinalIgnoreCase))
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
            ActualizarEncabezadoLista();
        }

        private static void AplicarMarcoNeon(Panel panel)
        {
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Panel p = (Panel)sender;
                Rectangle r = new Rectangle(1, 1, Math.Max(1, p.Width - 3), Math.Max(1, p.Height - 3));
                using (Pen resplandor = new Pen(Color.FromArgb(65, 0, 235, 245), 7F)) e.Graphics.DrawRectangle(resplandor, r);
                using (Pen borde = new Pen(Color.FromArgb(0, 225, 240), 2F)) e.Graphics.DrawRectangle(borde, r);
            };
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
                    if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate
                    {
                        // Al terminar una canción, continuar únicamente con la cola.
                        // No avanzar automáticamente por toda la biblioteca.
                        // Tampoco comprobar IsPlaying justo después de Play(): VLC puede
                        // tardar unos instantes en cambiar de estado y tapar la nueva canción.
                        if (cola.Items.Count > 0)
                        {
                            ReproducirSiguiente();
                        }
                        else
                        {
                            rutaActualReproduccion = "";
                            MostrarPortadaEnEspera();
                            ActualizarLetreroInferior();
                            reproducir.Text = "▶ REPRODUCIR";
                        }
                    });
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
                    "SELECT c.Titulo, IFNULL(a.Nombre,'Sin artista'), c.RutaArchivo, c.Tipo, IFNULL(g.Nombre,'') FROM Canciones c LEFT JOIN Artistas a ON a.Id=c.ArtistaId LEFT JOIN Generos g ON g.Id=c.GeneroId WHERE c.Activa=1 ORDER BY a.Nombre,c.Titulo;", conexion))
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
                            elemento.Name = Convert.ToString(lector[4]);
                            biblioteca.Items.Add(elemento);
                            todosLosElementos.Add(elemento);
                        }
                    }
                }
                tituloActual.Text = "Biblioteca cargada: " + biblioteca.Items.Count + " canciones y videos";
                try { aliasCarpetas = ServicioGeneros.AliasPorCarpeta(); } catch { aliasCarpetas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }
                ConstruirBotonesGenero();
                ConstruirMosaico();
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo cargar la biblioteca.\n" + ex.Message, "Biblioteca", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void MoverSeleccionCancion(int desplazamiento)
        {
            if (biblioteca.Items.Count == 0) return;
            
            if (panelPortadaPrincipal.Visible)
            {
                MostrarContenidoReproductor();
            }
            if (temporizadorInactividad != null)
            {
                temporizadorInactividad.Stop();
                temporizadorInactividad.Start();
            }

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
                rutaActualReproduccion = "";
                if (medioActual != null) medioActual.Dispose();
                medioActual = new Media(motor, new Uri(Path.GetFullPath(ruta)));
                if (!reproductor.Play(medioActual)) throw new InvalidOperationException("VLC no pudo iniciar la reproducción.");
                rutaActualReproduccion = Path.GetFullPath(ruta);
                tituloActual.Text = titulo + "  —  " + artista;
                reproducir.Text = "Ⅱ PAUSAR";

                lblTituloPortada.Text = titulo;
                lblArtistaPortada.Text = artista;
                
                string carpetaCancion = Path.GetDirectoryName(ruta);
                // Si el tema no tiene una portada válida, usar siempre la imagen del León.
                // Ambas imágenes se liberan al terminar de componer la caja del CD.
                using (Image caratulaEncontrada = BuscarPortada(carpetaCancion))
                using (Image portadaLeon = caratulaEncontrada == null ? CargarImagenLeon() : null)
                {
                    Image portadaFinal = caratulaEncontrada ?? portadaLeon;
                    EstablecerImagenCajaCD(portadaFinal);
                }

                // Mostrar inmediatamente: no esperar a que VLC actualice IsPlaying.
                // Si el usuario navega con las flechas, NotificarActividadUsuario la oculta
                // y el temporizador la vuelve a mostrar tras 3 segundos sin actividad.
                MostrarPortadaCancion();
                temporizadorInactividad.Stop();
                temporizadorInactividad.Start();
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo reproducir el archivo.\n" + ex.Message, "Error de reproducción", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private Image CargarImagenLeon()
        {
            if (!File.Exists(rutaLeonDefecto)) return null;
            using (Image origen = Image.FromFile(rutaLeonDefecto))
                return new Bitmap(origen);
        }

        private void EstablecerImagenCajaCD(Image caratula)
        {
            if (!File.Exists(rutaCajaVacia))
            {
                throw new FileNotFoundException("No se encontró PortadaVacia.png.png junto al programa.", rutaCajaVacia);
            }

            using (Image caja = Image.FromFile(rutaCajaVacia))
            using (Bitmap resultado = new Bitmap(caja.Width, caja.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(resultado))
            {
                // El PNG de la caja tiene zonas transparentes: rellenarlas con rojo
                // antes de dibujar conserva el fondo rojo visible alrededor del CD.
                g.Clear(Color.FromArgb(190, 0, 0));
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(caja, 0, 0, caja.Width, caja.Height);

                if (caratula != null)
                {
                    // La portada ocupa el espacio negro izquierdo; no invade el CD arcoíris.
                    Rectangle destino = new Rectangle(
                        (int)(caja.Width * 0.055),
                        (int)(caja.Height * 0.018),
                        (int)(caja.Width * 0.505),
                        (int)(caja.Height * 0.964));
                    g.DrawImage(caratula, destino);
                }
                pictureBoxPortada.Image = new Bitmap(resultado);
            }
            AjustarComposicionPortada();
        }

        private void MostrarPortadaEnEspera()
        {
            // La ruta se establece inmediatamente después de Play(). No depender de
            // IsPlaying aquí: VLC puede tardar unos instantes en actualizar ese estado.
            bool hayTemaActivo = !String.IsNullOrWhiteSpace(rutaActualReproduccion);

            if (!hayTemaActivo)
            {
                lblTituloPortada.Text = "";
                lblArtistaPortada.Text = "";
                lblDuracionPortada.Text = "";
                MostrarContenidoReproductor();
                return;
            }

            MostrarPortadaCancion();
        }

        private void MostrarPortadaCancion()
        {
            if (String.IsNullOrWhiteSpace(rutaActualReproduccion)) return;

            panelPortadaPrincipal.BackColor = Color.FromArgb(190, 0, 0);
            panelPortadaPrincipal.Dock = DockStyle.Fill;
            panelPortadaPrincipal.Visible = true;
            if (contenidoCentral != null) contenidoCentral.Visible = false;
            panelPortadaPrincipal.BringToFront();
            panelPortadaPrincipal.PerformLayout();
            pictureBoxPortada.BringToFront();
            lblTituloPortada.BringToFront();
            lblArtistaPortada.BringToFront();
            lblDuracionPortada.BringToFront();
            AjustarComposicionPortada();
        }

        private void MostrarContenidoReproductor()
        {
            panelPortadaPrincipal.Visible = false;
            if (contenidoCentral != null)
            {
                contenidoCentral.Dock = DockStyle.Fill;
                contenidoCentral.Visible = true;
                contenidoCentral.BringToFront();
            }
        }

        private void AjustarComposicionPortada()
        {
            if (panelPortadaPrincipal == null || pictureBoxPortada == null) return;
            int ancho = Math.Max(420, (int)(panelPortadaPrincipal.ClientSize.Width * 0.62));
            int alto = Math.Max(230, (int)(panelPortadaPrincipal.ClientSize.Height * 0.69));
            ancho = Math.Min(ancho, panelPortadaPrincipal.ClientSize.Width - 30);
            alto = Math.Min(alto, panelPortadaPrincipal.ClientSize.Height - 110);
            pictureBoxPortada.Bounds = new Rectangle(
                (panelPortadaPrincipal.ClientSize.Width - ancho) / 2,
                Math.Max(8, (panelPortadaPrincipal.ClientSize.Height - alto) / 2 - 18),
                ancho, alto);

            int margen = Math.Max(24, (int)(panelPortadaPrincipal.ClientSize.Width * 0.025));
            int y = panelPortadaPrincipal.ClientSize.Height - 82;
            lblTituloPortada.Bounds = new Rectangle(margen, y, panelPortadaPrincipal.ClientSize.Width - margen * 2, 30);
            lblArtistaPortada.Bounds = new Rectangle(margen, y + 29, panelPortadaPrincipal.ClientSize.Width - margen * 2, 22);
            lblDuracionPortada.Bounds = new Rectangle(margen, y + 50, panelPortadaPrincipal.ClientSize.Width - margen * 2, 22);
        }

        private void AgregarSeleccionALaCola()
        {
            // Al pulsar la tecla de agregar desde el buscador, ListView puede conservar
            // el elemento enfocado sin reportarlo en SelectedIndices. Recuperar esa fila.
            if (biblioteca.SelectedIndices.Count == 0 && biblioteca.FocusedItem != null)
            {
                biblioteca.FocusedItem.Selected = true;
            }

            if (biblioteca.SelectedIndices.Count == 0)
            {
                ActiveControl = biblioteca;
                MessageBox.Show(this, "Selecciona una canción de la lista y después presiona la tecla configurada como AGREGAR.", "Cola de reproducción", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int agregadas = 0;
            int duplicadas = 0;

            // SelectedIndices conserva el orden visual de la biblioteca.
            // Cada tema nuevo se agrega al final, sin interrumpir el que está sonando.
            foreach (int indice in biblioteca.SelectedIndices)
            {
                if (indice < 0 || indice >= biblioteca.Items.Count) continue;

                ListViewItem origen = biblioteca.Items[indice];
                string ruta = Convert.ToString(origen.Tag);
                if (String.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) continue;

                string rutaNormalizada;
                try { rutaNormalizada = Path.GetFullPath(ruta); }
                catch { rutaNormalizada = ruta; }

                bool yaExiste = !String.IsNullOrWhiteSpace(rutaActualReproduccion) &&
                    String.Equals(rutaActualReproduccion, rutaNormalizada, StringComparison.OrdinalIgnoreCase);

                for (int i = 0; !yaExiste && i < cola.Items.Count; i++)
                {
                    string rutaEnCola = Convert.ToString(cola.Items[i].Tag);
                    if (String.IsNullOrWhiteSpace(rutaEnCola)) continue;
                    try { rutaEnCola = Path.GetFullPath(rutaEnCola); } catch { }
                    yaExiste = String.Equals(rutaEnCola, rutaNormalizada, StringComparison.OrdinalIgnoreCase);
                }

                if (yaExiste)
                {
                    duplicadas++;
                    continue;
                }

                ListViewItem nuevo = new ListViewItem(origen.Text);
                nuevo.SubItems.Add(origen.SubItems[1].Text);
                nuevo.Tag = rutaNormalizada;
                cola.Items.Add(nuevo);
                agregadas++;
            }

            ActualizarContadorCola();

            if (ConfiguracionTeclado.MostrarMensajeAlAgregar)
            {
                string mensaje = agregadas == 1 ? "Se agregó 1 tema a la lista de reproducción." :
                    "Se agregaron " + agregadas + " temas a la lista de reproducción.";
                if (duplicadas > 0) mensaje += "\nSe omitieron " + duplicadas + " temas que ya estaban sonando o en la cola.";
                if (agregadas == 0 && duplicadas == 0) mensaje = "No se agregaron temas. Comprueba que los archivos existan.";
                MessageBox.Show(this, mensaje, "Lista de reproducción", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
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
            etiquetaCola.Text = "LISTA DE REPRODUCCIÓN (" + cola.Items.Count + ")";
            ActualizarLetreroInferior();
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
            // Siguiente siempre significa la siguiente canción agregada a la cola.
            // Nunca recorrer automáticamente la biblioteca completa.
            while (cola.Items.Count > 0)
            {
                ListViewItem elemento = cola.Items[0];
                string ruta = Convert.ToString(elemento.Tag);
                string titulo = elemento.Text;
                string artista = elemento.SubItems[1].Text;

                // Retirar el elemento de la cola antes de intentar reproducirlo
                // evita que un archivo inexistente bloquee el avance.
                cola.Items.RemoveAt(0);
                ActualizarContadorCola();

                if (String.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
                    continue;

                ReproducirArchivo(ruta, titulo, artista);
                SeleccionarEnBiblioteca(ruta);
                return;
            }

            // La cola terminó: detenerse y mostrar la pantalla de espera.
            // No saltar a la canción siguiente de la biblioteca.
            if (reproductor != null) reproductor.Stop();
            rutaActualReproduccion = "";
            reproducir.Text = "▶ REPRODUCIR";
            MostrarPortadaEnEspera();
            ActualizarLetreroInferior();
        }

        private void Reloj_Tick(object sender, EventArgs e)
        {
            if (reproductor == null) return;

            // EndReached puede llegar antes de que VLC actualice IsPlaying.
            // El reloj también vigila el estado final para recuperar la pantalla
            // de espera aunque el evento de fin no alcance a mostrarla.
            VLCState estado = reproductor.State;
            if (estado == VLCState.Ended || estado == VLCState.Stopped || estado == VLCState.NothingSpecial)
            {
                if (estado == VLCState.Ended && cola.Items.Count == 0)
                {
                    // Respaldo por si VLC no entrega EndReached: al finalizar,
                    // quitar siempre la pantalla roja y dejar disponible la biblioteca.
                    rutaActualReproduccion = "";
                    MostrarPortadaEnEspera();
                    ActualizarLetreroInferior();
                    reproducir.Text = "▶ REPRODUCIR";
                }
                else if (cola.Items.Count == 0 && !panelPortadaPrincipal.Visible)
                {
                    MostrarPortadaEnEspera();
                    ActualizarLetreroInferior();
                    reproducir.Text = "▶ REPRODUCIR";
                }
                return;
            }

            // Pausar no equivale a estar en espera: conserva la pantalla del reproductor.
            if (estado != VLCState.Playing) return;

            if (reproductor.Length > 0)
            {
                moviendoProgreso = true;
                progreso.Value = Math.Max(0, Math.Min(1000, (int)(reproductor.Position * 1000)));
                moviendoProgreso = false;

                string tiempoActual = FormatearTiempo(reproductor.Time);
                string tiempoTotal = FormatearTiempo(reproductor.Length);
                tiempo.Text = tiempoActual + " / " + tiempoTotal;

                if (lblDuracionPortada != null)
                    lblDuracionPortada.Text = "DURACIÓN: " + tiempoActual + " / " + tiempoTotal;

                if (letreroInferior != null && panelPortadaPrincipal.Visible)
                    letreroInferior.Texto = $"REPRODUCIENDO: {lblTituloPortada.Text} | ARTISTA: {lblArtistaPortada.Text} | TIEMPO: {tiempoActual} / {tiempoTotal}";
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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // La navegación configurable se procesa en KeyDown con KeyPreview,
            // para que tenga prioridad sobre el control que recibió el clic del mouse.
            // Aquí solo queda la tecla administrativa de acceso directo.
            Keys teclaBase = keyData & Keys.KeyCode;
            if (teclaBase == Keys.F12)
            {
                MostrarAdministracion();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void EjecutarAccion(AccionTeclado accion)
        {
            switch (accion)
            {
                case AccionTeclado.Izquierda: MoverPortada(-1); break;
                case AccionTeclado.Derecha: MoverPortada(1); break;
                case AccionTeclado.Arriba: MoverSeleccionCancion(-1); break;
                case AccionTeclado.Abajo: MoverSeleccionCancion(1); break;
                case AccionTeclado.Agregar: AgregarSeleccionALaCola(); break;
                case AccionTeclado.Cancelar: CancelarUltimo(); break;
                case AccionTeclado.SiguienteTema: ReproducirSiguiente(); break;
                case AccionTeclado.CambiarModo: CambiarGenero(); break;
                case AccionTeclado.Menu: MostrarMenuPrincipal(); break;
                case AccionTeclado.CerrarPrograma: if (ConfirmarSalida("¿Cerrar el programa?")) Application.Exit(); break;
                case AccionTeclado.SubirVolumen: CambiarVolumen(ConfiguracionTeclado.PasoVolumen); break;
                case AccionTeclado.BajarVolumen: CambiarVolumen(-ConfiguracionTeclado.PasoVolumen); break;
                case AccionTeclado.PantallaCompleta: WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; break;
                case AccionTeclado.ApagarPC: ApagarComputadora(); break;
                case AccionTeclado.Pausa: AlternarPausa(); break;
                case AccionTeclado.ReproducirTema: ReproducirSeleccion(); break;
                case AccionTeclado.Abecedario: AlternarAbc(); break;
                case AccionTeclado.MostrarOcultarCola: panelCola.Visible = !panelCola.Visible; break;
                case AccionTeclado.Buscar: busquedaMosaico.Focus(); busquedaMosaico.SelectAll(); break;
            }
        }

        private bool ConfirmarSalida(string pregunta)
        {
            if (!ConfiguracionTeclado.ConfirmarSalida) return true;
            return MessageBox.Show(this, pregunta, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void ApagarComputadora()
        {
            if (!ConfirmarSalida("¿Apagar la computadora?")) return;
            try { Process.Start("shutdown", "/s /t 10"); }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo apagar la PC.\n" + ex.Message); }
        }

        private void CambiarVolumen(int cambio)
        {
            int nuevo = Math.Max(0, Math.Min(100, volumen.Value + cambio));
            volumen.Value = nuevo;
            if (reproductor != null) reproductor.Volume = nuevo;
        }

        private void AlternarPausa()
        {
            if (reproductor == null) return;
            if (reproductor.State == VLCState.Playing) { reproductor.Pause(); reproducir.Text = "▶ CONTINUAR"; }
            else if (reproductor.State == VLCState.Paused) { reproductor.Pause(); reproducir.Text = "Ⅱ PAUSAR"; }
        }

        private void CancelarUltimo()
        {
            if (ConfiguracionTeclado.CancelarQuitaUltimo && cola.Items.Count > 0)
            {
                cola.Items.RemoveAt(cola.Items.Count - 1);
                ActualizarContadorCola();
                return;
            }
            if (TextoBuscado().Length > 0) ActualizarTextoBusqueda("");
        }

        private void MoverPaginaPortadas(int direccion)
        {
            if (tarjetas.Count == 0) return;
            int ultimaPagina = ((tarjetas.Count - 1) / PortadasPorPagina) * PortadasPorPagina;
            inicioPaginaPortadas = Math.Max(0, Math.Min(ultimaPagina, inicioPaginaPortadas + direccion * PortadasPorPagina));
            indicePortada = inicioPaginaPortadas;
            ActualizarPaginaPortadas();
            SeleccionarPortada(indicePortada);
        }

        private void ActualizarPaginaPortadas()
        {
            if (mosaico == null) return;
            int disponibles = Math.Max(1, mosaico.ClientSize.Width - mosaico.Padding.Horizontal - 14);
            int anchoTarjeta = Math.Max(70, Math.Min(220, (disponibles - PortadasPorPagina * 12) / PortadasPorPagina));
            int altoImagen = Math.Max(90, Math.Min(155, (int)(anchoTarjeta * 0.82)));
            for (int i = 0; i < tarjetas.Count; i++)
            {
                Panel tarjeta = tarjetas[i];
                tarjeta.Visible = i >= inicioPaginaPortadas && i < inicioPaginaPortadas + PortadasPorPagina;
                tarjeta.Width = anchoTarjeta;
                tarjeta.Height = altoImagen + 65;
                foreach (Control control in tarjeta.Controls)
                {
                    PictureBox imagen = control as PictureBox;
                    if (imagen != null) imagen.Height = altoImagen;
                }
            }
            if (tituloPortadas != null)
            {
                int primero = tarjetas.Count == 0 ? 0 : inicioPaginaPortadas + 1;
                int ultimo = Math.Min(tarjetas.Count, inicioPaginaPortadas + PortadasPorPagina);
                tituloPortadas.Text = "PORTADAS " + primero + "–" + ultimo + " DE " + tarjetas.Count;
            }
            mosaico.AutoScrollPosition = Point.Empty;
        }

        private void AjustarColumnasBiblioteca()
        {
            if (biblioteca.Columns.Count < 2) return;
            int ancho = Math.Max(320, biblioteca.ClientSize.Width - 18);
            biblioteca.Columns[1].Width = Math.Max(110, (int)(ancho * 0.30));
            biblioteca.Columns[0].Width = Math.Max(180, ancho - biblioteca.Columns[1].Width);
        }

        private void MoverPortada(int paso)
        {
            if (tarjetas.Count == 0) return;
            if (panelPortadaPrincipal.Visible)
            {
                MostrarContenidoReproductor();
            }
            if (temporizadorInactividad != null)
            {
                temporizadorInactividad.Stop();
                temporizadorInactividad.Start();
            }
            
            indicePortada = (indicePortada + paso + tarjetas.Count) % tarjetas.Count;
            inicioPaginaPortadas = (indicePortada / PortadasPorPagina) * PortadasPorPagina;
            ActualizarPaginaPortadas();
            ResaltarPortadas();
            MostrarCancionesDeAlbum(carpetasTarjetas[indicePortada]);
            ActualizarTituloPortadas();
        }

        private void SeleccionarPortada(int indice)
        {
            if (tarjetas.Count == 0)
            {
                biblioteca.Items.Clear();
                ActualizarEncabezadoLista();
                ActualizarTituloPortadas();
                return;
            }
            indice = Math.Max(0, Math.Min(tarjetas.Count - 1, indice));
            indicePortada = indice;
            inicioPaginaPortadas = (indice / PortadasPorPagina) * PortadasPorPagina;
            ActualizarPaginaPortadas();
            ResaltarPortadas();
            MostrarCancionesDeAlbum(carpetasTarjetas[indice]);
            ActualizarTituloPortadas();
        }

        private void ActualizarEncabezadoLista()
        {
            if (biblioteca.Columns.Count == 0) return;
            int total = biblioteca.Items.Count;
            int actual = biblioteca.SelectedIndices.Count > 0 ? biblioteca.SelectedIndices[0] + 1 : (total > 0 ? 1 : 0);
            biblioteca.Columns[0].Text = "ÁLBUM SELECCIONADO  •  CANCIÓN " + actual + " DE " + total;
        }

        private void ActualizarTituloPortadas()
        {
            if (tituloPortadas == null) return;
            List<string> modos = ListaModos();
            int posicion = modos.FindIndex(delegate(string v) { return IgualNormal(v, filtroTipo); }) + 1;
            int primero = tarjetas.Count == 0 ? 0 : inicioPaginaPortadas + 1;
            int ultimo = Math.Min(tarjetas.Count, inicioPaginaPortadas + PortadasPorPagina);
            tituloPortadas.Text = "PORTADAS " + primero + "–" + ultimo + " DE " + tarjetas.Count + "  •  GÉNERO " + posicion + " DE " + modos.Count;
        }

        private List<string> ListaModos()
        {
            List<string> modos = new List<string>();
            modos.Add("Todos");
            modos.AddRange(nombresGenero);
            return modos;
        }

        private static string Normalizar(string texto)
        {
            if (String.IsNullOrEmpty(texto)) return "";
            string descompuesto = texto.Normalize(System.Text.NormalizationForm.FormD);
            System.Text.StringBuilder limpio = new System.Text.StringBuilder();
            foreach (char c in descompuesto)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) limpio.Append(c);
            return limpio.ToString().Trim().ToUpperInvariant();
        }

        private static bool IgualNormal(string a, string b)
        {
            return Normalizar(a) == Normalizar(b);
        }

        private static string EtiquetaGenero(string nombre)
        {
            string mayusculas = nombre.ToUpperInvariant();
            return mayusculas == "MUSICA" ? "MÚSICA" : mayusculas;
        }

        private Button CrearBotonGenero(string valor, int ancho, int alto)
        {
            Button boton = new Button { Text = EtiquetaGenero(valor), Tag = valor, Width = ancho, Height = alto };
            Estilizar(boton);
            boton.ForeColor = Color.White;
            boton.Click += delegate { filtroTipo = valor; ConstruirMosaico(); };
            botonesTipo.Add(new KeyValuePair<Button, string>(boton, valor));
            return boton;
        }

        private void ConstruirBotonesGenero()
        {
            nombresGenero.Clear();
            try { foreach (GeneroInfo g in ServicioGeneros.ListarGeneros()) nombresGenero.Add(g.Nombre); }
            catch { }
            if (nombresGenero.Count == 0) { nombresGenero.Add("Karaoke"); nombresGenero.Add("Musica"); nombresGenero.Add("Video"); }

            for (int i = botonesTipo.Count - 1; i >= 0; i--)
            {
                Button viejo = botonesTipo[i].Key;
                if (viejo.Parent == filtros || viejo.Parent == listaEstilos)
                {
                    viejo.Parent.Controls.Remove(viejo);
                    viejo.Dispose();
                    botonesTipo.RemoveAt(i);
                }
            }
            foreach (string nombre in nombresGenero) filtros.Controls.Add(CrearBotonGenero(nombre, 132, 36));
            AplicarVisibilidadGeneros();
            foreach (string nombre in ListaModos())
            {
                Button estilo = CrearBotonGenero(nombre, 180, 43);
                estilo.Margin = new Padding(3, 5, 3, 5);
                estilo.FlatAppearance.BorderSize = 1;
                estilo.FlatAppearance.BorderColor = Color.FromArgb(0, 220, 240);
                listaEstilos.Controls.Add(estilo);
            }
            if (!ListaModos().Exists(delegate(string v) { return IgualNormal(v, filtroTipo); })) filtroTipo = "Todos";
            ResaltarTipos();
        }

        private void AplicarVisibilidadGeneros()
        {
            bool mostrar = ConfiguracionTeclado.MostrarListaGeneros;
            if (filtros != null) filtros.Visible = mostrar;
            if (listaEstilos != null) listaEstilos.Visible = mostrar;
            if (tituloEstilos != null) tituloEstilos.Visible = mostrar;
        }

        private void ResaltarTipos()
        {
            foreach (KeyValuePair<Button, string> par in botonesTipo)
            {
                bool elegido = IgualNormal(par.Value, filtroTipo);
                par.Key.BackColor = elegido ? Color.FromArgb(245, 190, 0) : Color.FromArgb(0, 115, 190);
                par.Key.ForeColor = elegido ? Color.Black : Color.White;
            }
            ActualizarTituloPortadas();
        }

        private void CambiarGenero()
        {
            List<string> modos = ListaModos();
            int actual = modos.FindIndex(delegate(string v) { return IgualNormal(v, filtroTipo); });
            if (ConfiguracionTeclado.IrSiguienteGeneroDirectamente && modos.Count > 1)
            {
                if (actual < 1) actual = 0;
                int siguiente = actual + 1;
                if (siguiente >= modos.Count) siguiente = 1;
                filtroTipo = modos[siguiente];
            }
            else filtroTipo = modos[(actual + 1) % modos.Count];
            ConstruirMosaico();
        }

        private string TextoBuscado()
        {
            return busquedaMosaico.Text == TextoBusqueda ? "" : busquedaMosaico.Text;
        }

        private void ActualizarTextoBusqueda(string nuevo)
        {
            if (String.IsNullOrEmpty(nuevo)) { busquedaMosaico.Text = TextoBusqueda; busquedaMosaico.ForeColor = Color.DimGray; }
            else { busquedaMosaico.Text = nuevo; busquedaMosaico.ForeColor = Color.Black; }
        }

        private void CrearBarraAbc()
        {
            barraAbc = new Panel { Dock = DockStyle.Bottom, Height = 120, BackColor = Color.FromArgb(2, 10, 30), Visible = false, Padding = new Padding(6) };
            AplicarMarcoNeon(barraAbc);
            flujoAbc = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.FromArgb(2, 10, 30) };
            List<string> teclas = new List<string>();
            foreach (char letra in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789") teclas.Add(letra.ToString());
            teclas.Add("ESPACIO"); teclas.Add("BORRAR"); teclas.Add("CERRAR");
            foreach (string tecla in teclas)
            {
                string valor = tecla;
                Button boton = new Button { Text = tecla, Width = tecla.Length > 1 ? 120 : 48, Height = 46, Margin = new Padding(3), TabStop = false };
                Estilizar(boton);
                boton.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
                boton.Click += delegate { indiceAbc = botonesAbc.IndexOf(boton); PulsarTeclaAbc(valor); ResaltarAbc(); };
                flujoAbc.Controls.Add(boton);
                botonesAbc.Add(boton);
            }
            barraAbc.Controls.Add(flujoAbc);
            ResaltarAbc();
        }

        private void ResaltarAbc()
        {
            for (int i = 0; i < botonesAbc.Count; i++)
            {
                bool elegido = i == indiceAbc;
                botonesAbc[i].BackColor = elegido ? Color.FromArgb(245, 190, 0) : Color.FromArgb(0, 105, 175);
                botonesAbc[i].ForeColor = elegido ? Color.Black : Color.White;
            }
        }

        private void AlternarAbc()
        {
            if (barraAbc.Visible) { CerrarAbc(); return; }
            barraAbc.Visible = true;
            indiceAbc = 0;
            ResaltarAbc();
        }

        private void CerrarAbc()
        {
            barraAbc.Visible = false;
            ActiveControl = biblioteca;
        }

        private void PulsarTeclaAbc(string tecla)
        {
            if (tecla == "CERRAR") { CerrarAbc(); return; }
            string actual = TextoBuscado();
            if (tecla == "ESPACIO") ActualizarTextoBusqueda(actual + " ");
            else if (tecla == "BORRAR") ActualizarTextoBusqueda(actual.Length > 0 ? actual.Substring(0, actual.Length - 1) : "");
            else ActualizarTextoBusqueda(actual + tecla.ToLowerInvariant());
        }

        private void MoverAbcVertical(int direccion)
        {
            Button actual = botonesAbc[indiceAbc];
            int filaObjetivo = -1;
            foreach (Button b in botonesAbc)
            {
                if (direccion > 0 && b.Top > actual.Top && (filaObjetivo == -1 || b.Top < filaObjetivo)) filaObjetivo = b.Top;
                if (direccion < 0 && b.Top < actual.Top && (filaObjetivo == -1 || b.Top > filaObjetivo)) filaObjetivo = b.Top;
            }
            if (filaObjetivo == -1) return;
            int mejor = indiceAbc, distancia = Int32.MaxValue;
            for (int i = 0; i < botonesAbc.Count; i++)
            {
                if (botonesAbc[i].Top != filaObjetivo) continue;
                int d = Math.Abs(botonesAbc[i].Left - actual.Left);
                if (d < distancia) { distancia = d; mejor = i; }
            }
            indiceAbc = mejor;
        }

        private bool ManejarTeclaAbc(AccionTeclado accion)
        {
            switch (accion)
            {
                case AccionTeclado.Izquierda: indiceAbc = (indiceAbc + botonesAbc.Count - 1) % botonesAbc.Count; ResaltarAbc(); return true;
                case AccionTeclado.Derecha: indiceAbc = (indiceAbc + 1) % botonesAbc.Count; ResaltarAbc(); return true;
                case AccionTeclado.Arriba: MoverAbcVertical(-1); ResaltarAbc(); return true;
                case AccionTeclado.Abajo: MoverAbcVertical(1); ResaltarAbc(); return true;
                case AccionTeclado.Agregar: PulsarTeclaAbc(botonesAbc[indiceAbc].Text); ResaltarAbc(); return true;
                case AccionTeclado.Cancelar:
                case AccionTeclado.Abecedario: CerrarAbc(); return true;
            }
            return false;
        }

        private void MostrarMenuPrincipal()
        {
            string seleccion;
            using (FormularioMenuPrincipal menu = new FormularioMenuPrincipal())
            {
                menu.ShowDialog(this);
                seleccion = menu.Seleccion;
            }
            switch (seleccion)
            {
                case "MUSICA":
                    {
                        using (FormularioMusica ventana = new FormularioMusica()) { ventana.ShowDialog(this); }
                        CargarBiblioteca();
                        break;
                    }
                case "LETREROS": MostrarConfigLetreros(); break;
                case "SKIN":
                    {
                        using (FormularioSkin ventana = new FormularioSkin()) { ventana.ShowDialog(this); }
                        break;
                    }
                case "TECLADO":
                    {
                        using (FormularioTeclado ventana = new FormularioTeclado()) { ventana.ShowDialog(this); }
                        break;
                    }
                case "SALIR": if (ConfirmarSalida("¿Cerrar el programa?")) Application.Exit(); break;
                case "APAGAR": ApagarComputadora(); break;
                case "ACERCA":
                    MessageBox.Show(this, "ROCKOLA KARAOKE DANIEL\nMenú: tecla M (configurable).\nAdministración: tecla F12.", "Acerca de...", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }

        private void LiberarMotor()
        {
            reloj.Stop();
            if (temporizadorAbecedario != null) { temporizadorAbecedario.Stop(); temporizadorAbecedario.Dispose(); temporizadorAbecedario = null; }
            if (reproductor != null) { reproductor.Stop(); reproductor.Dispose(); reproductor = null; }
            if (medioActual != null) { medioActual.Dispose(); medioActual = null; }
            if (motor != null) { motor.Dispose(); motor = null; }
        }
    }
}
