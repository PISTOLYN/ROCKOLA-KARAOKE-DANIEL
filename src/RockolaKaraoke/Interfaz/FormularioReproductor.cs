using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;
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
        private Color colorInterfaz = Color.FromArgb(0, 220, 240);
        private bool reproduccionActualEsVideo;
        private string filtroTipo = "Todos";
        private readonly ListView cola = new ListView();
        // La lista visible conserva el historial completo; esta colección contiene solo los temas pendientes.
        private readonly List<ListViewItem> cancionesPendientes = new List<ListViewItem>();
        private ListViewItem temaActualLista;
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
        private readonly Timer temporizadorAvisoAgregar = new Timer();
        private Panel panelAvisoAgregar;
        private Label textoAvisoAgregar;
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
        private Panel panelPublicidad;
        private Panel barraAbc;
        private FlowLayoutPanel flujoAbc;
        private FlowLayoutPanel listaEstilos;
        private const string TextoBusqueda = "Buscar canción o artista...";
        private readonly List<Button> botonesAbc = new List<Button>();
        private int indiceAbc;
        private readonly List<KeyValuePair<Button, string>> botonesTipo = new List<KeyValuePair<Button, string>>();
        private readonly List<string> nombresGenero = new List<string>();
        private readonly List<Panel> tarjetas = new List<Panel>();
        private readonly List<string> carpetasTarjetas = new List<string>();
        private readonly HashSet<string> miniaturasSolicitadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Image imagenBaseCajaCD;
        private readonly object candadoImagenCajaCD = new object();
        private int indicePortada;
        private int inicioPaginaPortadas;
        private const int PortadasPorPagina = 7;
        private Dictionary<string, string> aliasCarpetas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // --- VARIABLES PARA LA PORTADA ---
        private Panel panelPortadaPrincipal;
        private Control contenidoCentral;
        private PictureBox pictureBoxPortada;
        private Label lblTituloPortada;
        private Label lblArtistaPortada;
        private Label lblDuracionPortada;
        private string rutaCajaVacia = Path.Combine(Application.StartupPath, "PortadaVacia.png.png");
        private string rutaLeonDefecto = Path.Combine(Application.StartupPath, "LeonDefault.jpg.jpg");
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
            temporizadorAvisoAgregar.Interval = 1300;
            temporizadorAvisoAgregar.Tick += delegate
            {
                temporizadorAvisoAgregar.Stop();
                if (panelAvisoAgregar != null) panelAvisoAgregar.Visible = false;
            };
            Resize += delegate { CentrarAvisoAgregar(); };
            CargarColorInterfaz();
            CrearInterfaz();

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
                Button cerrar = new Button { Text = "CERRAR", Width = 340, Height = 38, Left = 45, Top = 190 };
                foreach (Button b in new Button[] { bibliotecaAdmin, cerrar })
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
                cerrar.Click += delegate { menu.Close(); };
                menu.Controls.Add(titulo);
                menu.Controls.Add(bibliotecaAdmin);
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

            Panel inferior = new Panel { Dock = DockStyle.Bottom, Height = 122, BackColor = Color.FromArgb(5, 10, 24), Padding = new Padding(6, 2, 6, 2) };
            tituloActual.Text = "No hay tema en reproducción";
            tituloActual.Dock = DockStyle.Fill; tituloActual.TextAlign = ContentAlignment.MiddleLeft;
            tituloActual.Padding = new Padding(10, 0, 0, 0); tituloActual.ForeColor = Color.White; tituloActual.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            progreso.Minimum = 0; progreso.Maximum = 1000; progreso.TickStyle = TickStyle.None; progreso.Dock = DockStyle.Fill; progreso.Height = 20;
            progreso.Scroll += delegate { if (reproductor != null && reproductor.IsSeekable && reproductor.Length > 0) reproductor.Position = progreso.Value / 1000f; };
            tiempo.Text = "00:00 / 00:00"; tiempo.Dock = DockStyle.Right; tiempo.Width = 135; tiempo.TextAlign = ContentAlignment.MiddleCenter; tiempo.ForeColor = Color.LightGray;
            Panel filaProgreso = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(5, 10, 24), Height = 24 };
            filaProgreso.Controls.Add(progreso); filaProgreso.Controls.Add(tiempo);
            FlowLayoutPanel controles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(8, 1, 5, 1), BackColor = Color.FromArgb(5, 10, 24) };
            anterior.Text = "◀ ANTERIOR"; reproducir.Text = "▶ REPRODUCIR"; siguiente.Text = "SIGUIENTE ▶"; detener.Text = "■ DETENER";
            foreach (Button b in new Button[] { anterior, reproducir, siguiente, detener }) { b.Size = new Size(118, 39); Estilizar(b); controles.Controls.Add(b); }
            Button mostrarLista = new Button { Text = "☷ LISTA", Size = new Size(100, 39) };
            Estilizar(mostrarLista); mostrarLista.Click += delegate { AlternarListaReproduccion(); }; controles.Controls.Add(mostrarLista);
            Label etiquetaVolumen = new Label { Text = "VOLUMEN", Width = 78, Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White };
            volumen.Minimum = 0; volumen.Maximum = 100; volumen.Value = 70; volumen.Width = 145; volumen.Height = 40; volumen.TickFrequency = 10;
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
                if (temaActualLista != null) { EstablecerEstadoTema(temaActualLista, "DETENIDA"); temaActualLista = null; }
                rutaActualReproduccion = ""; reproduccionActualEsVideo = false;
                reproducir.Text = "▶ REPRODUCIR";
                MostrarPortadaEnEspera(); ActualizarLetreroInferior();
                tiempo.Text = "00:00 / 00:00"; progreso.Value = 0;
            };
            volumen.Scroll += delegate { if (reproductor != null) reproductor.Volume = volumen.Value; };
            TableLayoutPanel distribucionPie = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.FromArgb(5, 10, 24) };
            distribucionPie.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            distribucionPie.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            distribucionPie.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            distribucionPie.Controls.Add(tituloActual, 0, 0); distribucionPie.Controls.Add(filaProgreso, 0, 1); distribucionPie.Controls.Add(controles, 0, 2);
            inferior.Controls.Add(distribucionPie);

            TableLayoutPanel centro = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(1, 5, 18), ColumnCount = 1, RowCount = 2, Padding = new Padding(5) };
            centro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            centro.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            centro.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
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

            busquedaMosaico.Dock = DockStyle.Top; busquedaMosaico.Height = 38;
            busquedaMosaico.Font = new Font("Segoe UI", 12F); busquedaMosaico.Margin = new Padding(8);
            busquedaMosaico.ForeColor = Color.DimGray; busquedaMosaico.Text = TextoBusqueda;
            busquedaMosaico.GotFocus += delegate { if (busquedaMosaico.Text == TextoBusqueda) { busquedaMosaico.Text = ""; busquedaMosaico.ForeColor = Color.Black; } };
            busquedaMosaico.LostFocus += delegate { if (String.IsNullOrWhiteSpace(busquedaMosaico.Text)) { busquedaMosaico.Text = TextoBusqueda; busquedaMosaico.ForeColor = Color.DimGray; } };
            busquedaMosaico.TextChanged += delegate { ConstruirMosaico(); };

            biblioteca.Visible = true; biblioteca.Dock = DockStyle.Fill; biblioteca.View = View.Details;
            biblioteca.FullRowSelect = true; biblioteca.HideSelection = false; biblioteca.GridLines = false;
            biblioteca.BackColor = Color.FromArgb(2, 7, 17); biblioteca.ForeColor = Color.White;
            biblioteca.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
            ImageList altoFilas = new ImageList { ImageSize = new Size(1, 50), ColorDepth = ColorDepth.Depth32Bit };
            altoFilas.Images.Add(new Bitmap(1, 50));
            biblioteca.SmallImageList = altoFilas;
            biblioteca.Columns.Clear(); biblioteca.Columns.Add("ÁLBUM SELECCIONADO  •  CANCIÓN 1 DE 15", 620); biblioteca.Columns.Add("ARTISTA", 260);
            biblioteca.OwnerDraw = true;
            biblioteca.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e) { e.Graphics.FillRectangle(new SolidBrush(colorInterfaz), e.Bounds); TextRenderer.DrawText(e.Graphics, e.Header.Text, new Font("Segoe UI", 15F, FontStyle.Bold), e.Bounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis); };
            biblioteca.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { e.DrawDefault = true; };
            // Cada clic sobre una canción la agrega a la lista de reproducción,
            // incluso si ya estaba seleccionada o si aparece repetida en la cola.
            // MouseUp permite volver a pulsar la misma fila y agregar otra copia.
            biblioteca.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                ListViewItem temaPulsado = biblioteca.GetItemAt(e.X, e.Y);
                if (temaPulsado == null) return;
                LimpiarSeleccionBiblioteca();
                temaPulsado.Selected = true;
                temaPulsado.Focused = true;
                AgregarSeleccionALaCola(false);
            };
            biblioteca.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
            {
                Color basico = e.ItemIndex % 2 == 0 ? Color.FromArgb(5, 15, 30) : Color.FromArgb(10, 22, 39);
                bool marcada = e.Item.Selected;
                Color tinta = Color.White;
                Rectangle area = e.Bounds;
                int sangria = e.ColumnIndex == 0 ? 44 : 0;
                using (SolidBrush fondoBase = new SolidBrush(basico)) { e.Graphics.FillRectangle(fondoBase, area); }
                if (marcada)
                {
                    // Franja gris de selección: sigue la canción al navegar arriba y abajo.
                    using (SolidBrush franjaSeleccion = new SolidBrush(Color.FromArgb(105, 120, 137))) { e.Graphics.FillRectangle(franjaSeleccion, area.X + sangria, area.Y + 2, area.Width - sangria, area.Height - 4); }
                }
                if (e.ColumnIndex == 0)
                {
                    int tamanoMarca = marcada ? 26 : 16;
                    Rectangle marca = new Rectangle(area.X + 8 + (26 - tamanoMarca) / 2, area.Y + (area.Height - tamanoMarca) / 2, tamanoMarca, tamanoMarca);
                    if (marcada)
                    {
                        using (SolidBrush punto = new SolidBrush(colorInterfaz)) { e.Graphics.FillEllipse(punto, marca); }
                    }
                    else
                    {
                        using (SolidBrush cuadro = new SolidBrush(colorInterfaz)) { e.Graphics.FillRectangle(cuadro, marca); }
                    }
                }
                Rectangle zonaTexto = new Rectangle(area.X + sangria, area.Y, area.Width - sangria, area.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, biblioteca.Font, zonaTexto, tinta, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            biblioteca.SelectedIndexChanged += delegate { ActualizarEncabezadoLista(); };
            biblioteca.Resize += delegate { AjustarColumnasBiblioteca(); };

            lado.Controls.Add(biblioteca); lado.Controls.Add(busquedaMosaico);
            
            Panel zonaSuperior = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(2, 8, 24), Padding = new Padding(4) };
            Panel cuadroPortadas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(3, 10, 27), Padding = new Padding(4), Margin = new Padding(3) };
            AplicarMarcoNeon(cuadroPortadas);
            tituloPortadas = new Label { Text = "PORTADAS", Dock = DockStyle.Top, Height = 27, TextAlign = ContentAlignment.MiddleCenter, ForeColor = colorInterfaz, Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
            listaEstilos = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(5, 4, 5, 2), BackColor = Color.FromArgb(3, 10, 27) };
            mosaico.Dock = DockStyle.Fill; mosaico.AutoScroll = false; mosaico.WrapContents = false;
            mosaico.FlowDirection = FlowDirection.LeftToRight; mosaico.Padding = new Padding(6, 4, 6, 4); mosaico.BackColor = Color.FromArgb(3, 10, 27);
            cuadroPortadas.Controls.Add(mosaico); cuadroPortadas.Controls.Add(listaEstilos); cuadroPortadas.Controls.Add(tituloPortadas);
            AplicarVisibilidadGeneros();
            zonaSuperior.Controls.Add(cuadroPortadas);
            marcoSuperior.Controls.Add(zonaSuperior);
            distribucionInferior.Controls.Add(lado, 0, 0);
            Panel panelDerecho = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(2, 7, 17), Padding = new Padding(7) };
            panelPublicidad = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black, BorderStyle = BorderStyle.None };
            pantalla.Dock = DockStyle.Fill; pantalla.BackColor = Color.Black;
            panelPublicidad.Controls.Add(pantalla);
            panelCola = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 12, 25) };
            etiquetaCola.Text = "LISTA DE REPRODUCCIÓN • VACÍA"; etiquetaCola.Dock = DockStyle.Top; etiquetaCola.Height = 38;
            etiquetaCola.TextAlign = ContentAlignment.MiddleLeft; etiquetaCola.Padding = new Padding(9, 0, 0, 0); etiquetaCola.ForeColor = Color.Gold;
            etiquetaCola.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            cola.Dock = DockStyle.Fill; cola.View = View.Details; cola.FullRowSelect = true; cola.GridLines = false;
            cola.HideSelection = false; cola.BackColor = Color.FromArgb(4, 12, 25); cola.ForeColor = Color.White;
            cola.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
            ImageList altoFilasCola = new ImageList { ImageSize = new Size(1, 44), ColorDepth = ColorDepth.Depth32Bit };
            altoFilasCola.Images.Add(new Bitmap(1, 44));
            cola.SmallImageList = altoFilasCola;
            cola.Columns.Clear(); cola.Columns.Add("TEMA", 205); cola.Columns.Add("ARTISTA", 125); cola.Columns.Add("ESTADO", 115);
            cola.DoubleClick += delegate { ReproducirElementoDeCola(); };
            FlowLayoutPanel accionesCola = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(2) };
            Button agregar = new Button { Text = "+ AGREGAR", Width = 92, Height = 32 };
            Button quitar = new Button { Text = "QUITAR", Width = 78, Height = 32 };
            Button subir = new Button { Text = "▲", Width = 42, Height = 32 };
            Button bajar = new Button { Text = "▼", Width = 42, Height = 32 };
            Button vaciar = new Button { Text = "VACIAR", Width = 78, Height = 32 };
            foreach (Button b in new Button[] { agregar, quitar, subir, bajar, vaciar }) { Estilizar(b); accionesCola.Controls.Add(b); }
            agregar.Click += delegate { AgregarSeleccionALaCola(); }; quitar.Click += delegate { QuitarDeLaCola(); }; subir.Click += delegate { MoverEnCola(-1); };
            bajar.Click += delegate { MoverEnCola(1); }; vaciar.Click += delegate { VaciarPendientes(); };
            panelCola.Controls.Add(cola); panelCola.Controls.Add(accionesCola); panelCola.Controls.Add(etiquetaCola);
            // La lista inicia oculta y se despliega desde el panel derecho al pulsar su tecla configurable.
            panelCola.Visible = false;
            panelDerecho.Controls.Add(panelPublicidad);
            panelDerecho.Controls.Add(panelCola);
            panelCola.Visible = false;
            panelPublicidad.BringToFront();
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

            // La portada roja permanece fija durante la reproducción de audio.
            // El movimiento del mouse ya no cambia de vista.
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
            foreach (Control controlAnterior in mosaico.Controls)
            {
                foreach (Control hijo in controlAnterior.Controls)
                {
                    PictureBox imagenAnterior = hijo as PictureBox;
                    if (imagenAnterior != null && imagenAnterior.Image != null)
                    {
                        imagenAnterior.Image.Dispose();
                        imagenAnterior.Image = null;
                    }
                }
                controlAnterior.Dispose();
            }
            mosaico.Controls.Clear();
            miniaturasSolicitadas.Clear();
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
                    string tipo = Convert.ToString(elemento.ToolTipText);
                    string genero = Convert.ToString(elemento.Name);
                    if (!IgualNormal(tipo, filtroTipo) && !IgualNormal(genero, filtroTipo)) continue;
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

                Panel tarjeta = new Panel { Width = 178, Height = 205, Margin = new Padding(9, 5, 9, 5), BackColor = Color.FromArgb(0, 62, 125), Cursor = Cursors.Hand, Tag = posicion };
                tarjeta.Paint += delegate(object sender, PaintEventArgs e)
                {
                    Panel p = (Panel)sender;
                    bool elegida = Convert.ToInt32(p.Tag) == indicePortada;
                    using (Pen borde = new Pen(elegida ? Color.FromArgb(255, 215, 0) : colorInterfaz, elegida ? 5F : 3F)) e.Graphics.DrawRectangle(borde, 1, 1, p.Width - 3, p.Height - 3);
                };
                PictureBox portada = new PictureBox { Dock = DockStyle.Top, Height = 145, BackColor = Color.FromArgb(13, 32, 75), SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand };
                portada.Tag = carpeta;
                portada.Paint += delegate(object sender, PaintEventArgs e)
                {
                    PictureBox cuadro = (PictureBox)sender;
                    if (cuadro.Image != null) return;
                    Rectangle r = cuadro.ClientRectangle;
                    using (System.Drawing.Drawing2D.LinearGradientBrush grad = new System.Drawing.Drawing2D.LinearGradientBrush(r, Color.FromArgb(18, 38, 105), Color.FromArgb(135, 25, 115), 35F))
                        e.Graphics.FillRectangle(grad, r);
                    using (Font f = new Font("Segoe UI Symbol", 48F, FontStyle.Bold))
                    using (SolidBrush b = new SolidBrush(Color.White))
                        e.Graphics.DrawString("♫", f, b, 58, 28);
                };
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
            SeleccionarPortada(0);
        }

        private void ResaltarPortadas()
        {
            int fin = Math.Min(tarjetas.Count, inicioPaginaPortadas + PortadasPorPagina);
            for (int i = inicioPaginaPortadas; i < fin; i++)
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
                        using (Image original = Image.FromStream(fs)) return CrearPortadaOptimizada(original);
                    }
                }
                foreach (string archivo in Directory.GetFiles(carpeta))
                {
                    string ext = Path.GetExtension(archivo).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                    {
                        using (FileStream fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (Image original = Image.FromStream(fs)) return CrearPortadaOptimizada(original);
                    }
                }
            }
            catch { }
            return null;
        }

        private static Image CrearPortadaOptimizada(Image original)
        {
            double escala = Math.Min(1.0, 1000.0 / Math.Max(original.Width, original.Height));
            int ancho = Math.Max(1, (int)Math.Round(original.Width * escala));
            int alto = Math.Max(1, (int)Math.Round(original.Height * escala));
            Bitmap reducida = new Bitmap(ancho, alto, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(reducida))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                g.DrawImage(original, new Rectangle(0, 0, ancho, alto));
            }
            return reducida;
        }

        // Decodifica las miniaturas en segundo plano y las reduce para el mosaico.
        private Image BuscarMiniaturaPortada(string carpeta)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(carpeta) || !Directory.Exists(carpeta)) return null;
                string[] nombres = new string[] { "cover.jpg", "cover.png", "folder.jpg", "folder.png", "front.jpg", "front.png", "caratula.jpg", "caratula.png", "album.jpg", "album.png" };
                string archivoElegido = null;
                foreach (string nombre in nombres)
                {
                    string candidato = Path.Combine(carpeta, nombre);
                    if (File.Exists(candidato)) { archivoElegido = candidato; break; }
                }
                if (archivoElegido == null)
                {
                    foreach (string candidato in Directory.GetFiles(carpeta))
                    {
                        string ext = Path.GetExtension(candidato).ToLowerInvariant();
                        if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                        { archivoElegido = candidato; break; }
                    }
                }
                if (archivoElegido == null) return null;
                using (FileStream fs = new FileStream(archivoElegido, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image original = Image.FromStream(fs))
                {
                    double escala = Math.Min(320.0 / original.Width, 220.0 / original.Height);
                    int ancho = Math.Max(1, (int)Math.Round(original.Width * escala));
                    int alto = Math.Max(1, (int)Math.Round(original.Height * escala));
                    Bitmap miniatura = new Bitmap(ancho, alto, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(miniatura))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                        g.DrawImage(original, new Rectangle(0, 0, ancho, alto));
                    }
                    return miniatura;
                }
            }
            catch { return null; }
        }

        private void CargarMiniaturaPortadaAsync(PictureBox cuadro, string carpeta)
        {
            if (cuadro == null || cuadro.IsDisposed || !miniaturasSolicitadas.Add(carpeta)) return;
            cuadro.Tag = carpeta;
            Task.Run(() => BuscarMiniaturaPortada(carpeta)).ContinueWith(tarea =>
            {
                if (tarea.IsFaulted || tarea.IsCanceled) return;
                Image miniatura = tarea.Result;
                if (miniatura == null) return;
                if (IsDisposed || !IsHandleCreated) { miniatura.Dispose(); return; }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (IsDisposed || cuadro.IsDisposed ||
                            !String.Equals(Convert.ToString(cuadro.Tag), carpeta, StringComparison.OrdinalIgnoreCase))
                        {
                            miniatura.Dispose();
                            return;
                        }
                        Image anterior = cuadro.Image;
                        cuadro.Image = miniatura;
                        if (anterior != null) anterior.Dispose();
                        cuadro.Invalidate();
                    });
                }
                catch { miniatura.Dispose(); }
            }, TaskScheduler.Default);
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

        private void AplicarMarcoNeon(Panel panel)
        {
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Panel p = (Panel)sender;
                Rectangle r = new Rectangle(1, 1, Math.Max(1, p.Width - 3), Math.Max(1, p.Height - 3));
                using (Pen resplandor = new Pen(Color.FromArgb(65, colorInterfaz), 7F)) e.Graphics.DrawRectangle(resplandor, r);
                using (Pen borde = new Pen(colorInterfaz, 2F)) e.Graphics.DrawRectangle(borde, r);
            };
        }

        private void Estilizar(Button boton)
        {
            boton.FlatStyle = FlatStyle.Flat; boton.FlatAppearance.BorderSize = 1; boton.FlatAppearance.BorderColor = colorInterfaz;
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
                        if (temaActualLista != null) EstablecerEstadoTema(temaActualLista, "REPRODUCIDA");
                        temaActualLista = null;
                        rutaActualReproduccion = "";
                        if (cancionesPendientes.Count > 0) ReproducirSiguiente();
                        else
                        {
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
            
int indice = biblioteca.SelectedIndices.Count > 0 ? biblioteca.SelectedIndices[0] : 0;
            indice = Math.Max(0, Math.Min(biblioteca.Items.Count - 1, indice + desplazamiento));
            LimpiarSeleccionBiblioteca();
            biblioteca.Items[indice].Selected = true;
            biblioteca.Items[indice].Focused = true;
            biblioteca.Items[indice].EnsureVisible();
            // Al recorrer la biblioteca con las flechas, cada tema seleccionado
            // se incorpora inmediatamente a la cola, sin ventanas emergentes.
            AgregarSeleccionALaCola(false);
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
                reproduccionActualEsVideo = EsArchivoVideo(ruta);
                if (reproduccionActualEsVideo)
                    MostrarContenidoReproductor();
                else
                {
                    MostrarPortadaCancion();
                    CargarPortadaCancionAsync(rutaActualReproduccion, carpetaCancion);
                }
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo reproducir el archivo.\n" + ex.Message, "Error de reproducción", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private Image CargarImagenLeon()
        {
            if (!File.Exists(rutaLeonDefecto)) return null;
            using (Image origen = Image.FromFile(rutaLeonDefecto))
                return new Bitmap(origen);
        }

        private void CargarPortadaCancionAsync(string rutaCancion, string carpetaCancion)
        {
            Task.Run(() =>
            {
                using (Image caratulaEncontrada = BuscarPortada(carpetaCancion))
                using (Image portadaLeon = caratulaEncontrada == null ? CargarImagenLeon() : null)
                    return CrearImagenCajaCD(caratulaEncontrada ?? portadaLeon);
            }).ContinueWith(tarea =>
            {
                if (tarea.IsFaulted || tarea.IsCanceled) return;
                Bitmap nueva = tarea.Result;
                if (IsDisposed || !IsHandleCreated) { nueva.Dispose(); return; }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (IsDisposed || pictureBoxPortada == null || pictureBoxPortada.IsDisposed ||
                            !String.Equals(rutaActualReproduccion, rutaCancion, StringComparison.OrdinalIgnoreCase))
                        {
                            nueva.Dispose();
                            return;
                        }
                        Image anterior = pictureBoxPortada.Image;
                        pictureBoxPortada.Image = nueva;
                        if (anterior != null) anterior.Dispose();
                        AjustarComposicionPortada();
                    });
                }
                catch { nueva.Dispose(); }
            }, TaskScheduler.Default);
        }

        private Bitmap CrearImagenCajaCD(Image caratula)
        {
            lock (candadoImagenCajaCD)
            {
                Image caja = ObtenerImagenBaseCajaCD();
                using (Bitmap resultado = new Bitmap(caja.Width, caja.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(resultado))
                {
                    g.Clear(Color.FromArgb(190, 0, 0));
                    g.DrawImage(caja, 0, 0, caja.Width, caja.Height);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    if (caratula != null)
                    {
                        Rectangle destino = new Rectangle(
                            (int)(caja.Width * 0.055),
                            (int)(caja.Height * 0.018),
                            (int)(caja.Width * 0.505),
                            (int)(caja.Height * 0.964));
                        g.DrawImage(caratula, destino);
                    }
                    return new Bitmap(resultado);
                }
            }
        }

        private Image ObtenerImagenBaseCajaCD()
        {
            if (imagenBaseCajaCD != null) return imagenBaseCajaCD;
            if (!File.Exists(rutaCajaVacia))
                throw new FileNotFoundException("No se encontró PortadaVacia.png.png junto al programa.", rutaCajaVacia);
            using (Image original = Image.FromFile(rutaCajaVacia))
            {
                double escala = Math.Min(1.0, 1400.0 / Math.Max(original.Width, original.Height));
                int ancho = Math.Max(1, (int)Math.Round(original.Width * escala));
                int alto = Math.Max(1, (int)Math.Round(original.Height * escala));
                Bitmap baseReducida = new Bitmap(ancho, alto, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(baseReducida))
                {
                    g.Clear(Color.FromArgb(190, 0, 0));
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    g.DrawImage(original, new Rectangle(0, 0, ancho, alto));
                }
                imagenBaseCajaCD = baseReducida;
            }
            return imagenBaseCajaCD;
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

            if (reproduccionActualEsVideo) MostrarContenidoReproductor();
            else MostrarPortadaCancion();
        }

        private void MostrarPortadaCancion()
        {
            if (String.IsNullOrWhiteSpace(rutaActualReproduccion) || reproduccionActualEsVideo) return;

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

        private void AgregarSeleccionALaCola(bool mostrarMensaje = true)
        {
            if (biblioteca.SelectedIndices.Count == 0 && biblioteca.FocusedItem != null)
                biblioteca.FocusedItem.Selected = true;
            if (biblioteca.SelectedIndices.Count == 0) return;

            int agregadas = 0;
            foreach (int indice in biblioteca.SelectedIndices)
            {
                if (indice < 0 || indice >= biblioteca.Items.Count) continue;
                ListViewItem origen = biblioteca.Items[indice];
                string ruta = Convert.ToString(origen.Tag);
                if (String.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) continue;

                string rutaNormalizada;
                try { rutaNormalizada = Path.GetFullPath(ruta); }
                catch { rutaNormalizada = ruta; }

                // Cada OK agrega una entrada nueva; se permiten canciones repetidas.
                ListViewItem nuevo = new ListViewItem(origen.Text);
                nuevo.SubItems.Add(origen.SubItems[1].Text);
                nuevo.SubItems.Add("EN ESPERA");
                nuevo.Tag = rutaNormalizada;
                cola.Items.Add(nuevo);
                cancionesPendientes.Add(nuevo);
                agregadas++;
            }

            ActualizarContadorCola();

            // Si no hay canción activa, el primer tema agregado comienza inmediatamente.
            if (String.IsNullOrWhiteSpace(rutaActualReproduccion) ||
                reproductor == null ||
                (reproductor.State != VLCState.Playing && reproductor.State != VLCState.Paused))
            {
                if (cancionesPendientes.Count > 0) ReproducirSiguiente();
            }

            if (mostrarMensaje && ConfiguracionTeclado.MostrarMensajeAlAgregar && agregadas > 0)
                MostrarAvisoTemaAgregado();
        }

        private void MostrarAvisoTemaAgregado()
        {
            // Aviso visual breve y no modal: no pausa VLC ni bloquea teclado/controles.
            if (panelAvisoAgregar == null)
            {
                panelAvisoAgregar = new Panel
                {
                    Size = new Size(390, 76),
                    BackColor = Color.FromArgb(0, 145, 125),
                    BorderStyle = BorderStyle.FixedSingle,
                    Visible = false
                };
                textoAvisoAgregar = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "TEMA AGREGADO",
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 21F, FontStyle.Bold)
                };
                panelAvisoAgregar.Controls.Add(textoAvisoAgregar);
                Controls.Add(panelAvisoAgregar);
            }

            CentrarAvisoAgregar();
            panelAvisoAgregar.Visible = true;
            panelAvisoAgregar.BringToFront();
            temporizadorAvisoAgregar.Stop();
            temporizadorAvisoAgregar.Start();
        }

        private void CentrarAvisoAgregar()
        {
            if (panelAvisoAgregar == null) return;
            panelAvisoAgregar.Left = Math.Max(0, (ClientSize.Width - panelAvisoAgregar.Width) / 2);
            panelAvisoAgregar.Top = Math.Max(0, (ClientSize.Height - panelAvisoAgregar.Height) / 2);
        }

        private void EstablecerEstadoTema(ListViewItem tema, string estado)
        {
            if (tema == null || tema.ListView != cola) return;
            while (tema.SubItems.Count < 3) tema.SubItems.Add("");
            tema.SubItems[2].Text = estado;
            tema.ForeColor = estado == "SONANDO" ? Color.Lime :
                estado == "EN ESPERA" ? Color.White :
                estado == "CANCELADA" ? Color.OrangeRed : Color.Silver;
        }

        private void ReproducirElementoDeCola()
        {
            if (cola.SelectedItems.Count == 0) return;
            ListViewItem elegido = cola.SelectedItems[0];
            if (!cancionesPendientes.Contains(elegido)) return;

            // Doble clic adelanta el tema elegido en la lista, pero nunca corta el actual.
            cancionesPendientes.Remove(elegido);
            cancionesPendientes.Insert(0, elegido);
            if (String.IsNullOrWhiteSpace(rutaActualReproduccion) ||
                reproductor == null ||
                (reproductor.State != VLCState.Playing && reproductor.State != VLCState.Paused))
                ReproducirSiguiente();
            ActualizarContadorCola();
        }

        private void QuitarDeLaCola()
        {
            if (cola.SelectedItems.Count == 0) return;
            ListViewItem elegido = cola.SelectedItems[0];
            if (!cancionesPendientes.Remove(elegido)) return;
            EstablecerEstadoTema(elegido, "QUITADA");
            ActualizarContadorCola();
        }

        private void MoverEnCola(int desplazamiento)
        {
            if (cola.SelectedItems.Count == 0) return;
            ListViewItem elegido = cola.SelectedItems[0];
            int indice = cancionesPendientes.IndexOf(elegido);
            int destino = indice + desplazamiento;
            if (indice < 0 || destino < 0 || destino >= cancionesPendientes.Count) return;
            cancionesPendientes.RemoveAt(indice);
            cancionesPendientes.Insert(destino, elegido);
            elegido.Selected = true;
            elegido.EnsureVisible();
            ActualizarContadorCola();
        }

        private void VaciarPendientes()
        {
            foreach (ListViewItem tema in cancionesPendientes)
                EstablecerEstadoTema(tema, "QUITADA");
            cancionesPendientes.Clear();
            ActualizarContadorCola();
        }

        private void ActualizarContadorCola()
        {
            etiquetaCola.Text = "LISTA DE REPRODUCCIÓN (" + cola.Items.Count + ") • EN ESPERA: " + cancionesPendientes.Count;
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
            // Se avanza solo por los temas agregados explícitamente a la lista.
            while (cancionesPendientes.Count > 0)
            {
                ListViewItem elemento = cancionesPendientes[0];
                cancionesPendientes.RemoveAt(0);
                string ruta = Convert.ToString(elemento.Tag);

                if (String.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
                {
                    EstablecerEstadoTema(elemento, "NO DISPONIBLE");
                    continue;
                }

                if (temaActualLista != null && temaActualLista != elemento)
                    EstablecerEstadoTema(temaActualLista, "REPRODUCIDA");

                temaActualLista = elemento;
                EstablecerEstadoTema(elemento, "SONANDO");
                ActualizarContadorCola();
                ReproducirArchivo(ruta, elemento.Text, elemento.SubItems[1].Text);
                SeleccionarEnBiblioteca(ruta);
                return;
            }

            if (temaActualLista != null)
                EstablecerEstadoTema(temaActualLista, "REPRODUCIDA");
            temaActualLista = null;
            if (reproductor != null) reproductor.Stop();
            rutaActualReproduccion = "";
            reproducir.Text = "▶ REPRODUCIR";
            MostrarPortadaEnEspera();
            ActualizarContadorCola();
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
                if (estado == VLCState.Ended && cancionesPendientes.Count == 0)
                {
                    // Respaldo por si VLC no entrega EndReached: al finalizar,
                    // quitar siempre la pantalla roja y dejar disponible la biblioteca.
                    rutaActualReproduccion = "";
                    MostrarPortadaEnEspera();
                    ActualizarLetreroInferior();
                    reproducir.Text = "▶ REPRODUCIR";
                }
                else if (cancionesPendientes.Count == 0 && !panelPortadaPrincipal.Visible)
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

        private TextBoxBase ObtenerCuadroTextoConFoco(Control raiz)
        {
            if (raiz == null || !raiz.ContainsFocus) return null;
            TextBoxBase cuadro = raiz as TextBoxBase;
            if (cuadro != null && cuadro.Focused) return cuadro;

            foreach (Control hijo in raiz.Controls)
            {
                if (!hijo.ContainsFocus) continue;
                cuadro = ObtenerCuadroTextoConFoco(hijo);
                if (cuadro != null) return cuadro;
            }
            return null;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Centralizar aquí las teclas configurables evita que ListView, botones,
            // TrackBar u otros controles consuman las flechas antes que la Rockola.
            Keys teclaBase = keyData & Keys.KeyCode;

            // No interceptar combinaciones del sistema ni escritura normal en búsqueda.
            if ((keyData & (Keys.Control | Keys.Alt)) != Keys.None)
                return base.ProcessCmdKey(ref msg, keyData);

            if (teclaBase == Keys.F12)
            {
                MostrarAdministracion();
                return true;
            }

            // Detectar también cuadros de texto dentro de paneles y contenedores.
            // Si el usuario está escribiendo una búsqueda, no convertir sus letras
            // en atajos del reproductor.
            TextBoxBase cuadroConFoco = ObtenerCuadroTextoConFoco(this);
            if (cuadroConFoco != null)
            {
                // CANCELAR es una acción de la rockola: debe funcionar incluso
                // cuando el foco está en el buscador y no debe quedarse como Escape
                // para salir del cuadro de texto.
                AccionTeclado? accionEnBusqueda = ConfiguracionTeclado.Buscar(teclaBase);
                if (accionEnBusqueda == AccionTeclado.Cancelar)
                {
EjecutarAccion(AccionTeclado.Cancelar);
                    return true;
                }
                if (teclaBase == Keys.Escape)
                {
                    biblioteca.Focus();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            // Windows puede repetir WM_KEYDOWN mientras se mantiene pulsada una tecla.
            // Ignorar repeticiones evita agregar varias veces una canción o ejecutar
            // varias acciones sensibles por una sola pulsación sostenida.
            bool repeticion = (msg.Msg == 0x0100 || msg.Msg == 0x0104) &&
                ((((long)msg.LParam) & 0x40000000L) != 0);
            if (repeticion) return true;

            AccionTeclado? accion = ConfiguracionTeclado.Buscar(teclaBase);
            if (!accion.HasValue)
                return base.ProcessCmdKey(ref msg, keyData);

            if (barraAbc != null && barraAbc.Visible && ManejarTeclaAbc(accion.Value))
                return true;

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
                return true;
            }

            EjecutarAccion(accion.Value);
            return true;
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
                case AccionTeclado.MostrarOcultarCola: AlternarListaReproduccion(); break;
                case AccionTeclado.Buscar: AlternarAbc(); break;
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
            // CANCELAR detiene la canción actual y pasa inmediatamente a la siguiente.
            if (temaActualLista != null && reproductor != null &&
                (reproductor.State == VLCState.Playing || reproductor.State == VLCState.Paused))
            {
                reproductor.Stop();
                EstablecerEstadoTema(temaActualLista, "CANCELADA");
                temaActualLista = null;
                rutaActualReproduccion = "";
                if (cancionesPendientes.Count > 0) ReproducirSiguiente();
                else
                {
                    reproducir.Text = "▶ REPRODUCIR";
                    MostrarPortadaEnEspera();
                    ActualizarContadorCola();
                }
                return;
            }

            // Si no hay canción sonando, cancelar elimina el último tema pendiente.
            if (ConfiguracionTeclado.CancelarQuitaUltimo && cancionesPendientes.Count > 0)
            {
                ListViewItem ultimo = cancionesPendientes[cancionesPendientes.Count - 1];
                cancionesPendientes.RemoveAt(cancionesPendientes.Count - 1);
                EstablecerEstadoTema(ultimo, "CANCELADA");
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
            SeleccionarPortada(indicePortada);
        }

        private void ActualizarPaginaPortadas()
        {
            if (mosaico == null) return;
            int disponibles = Math.Max(1, mosaico.ClientSize.Width - mosaico.Padding.Horizontal - 14);
            int anchoTarjeta = Math.Max(105, Math.Min(270, (disponibles - PortadasPorPagina * 12) / PortadasPorPagina));
            int altoImagen = Math.Max(115, Math.Min(225, (int)(anchoTarjeta * 0.94)));
            for (int i = 0; i < tarjetas.Count; i++)
            {
                Panel tarjeta = tarjetas[i];
                tarjeta.Visible = i >= inicioPaginaPortadas && i < inicioPaginaPortadas + PortadasPorPagina;
                if (tarjeta.Visible)
                {
                    tarjeta.Width = anchoTarjeta;
                    tarjeta.Height = altoImagen + 65;
                    foreach (Control control in tarjeta.Controls)
                    {
                        PictureBox imagen = control as PictureBox;
                        if (imagen != null)
                        {
                            imagen.Height = altoImagen;
                            if (imagen.Image == null)
                                CargarMiniaturaPortadaAsync(imagen, carpetasTarjetas[i]);
                        }
                    }
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
            int primero = tarjetas.Count == 0 ? 0 : inicioPaginaPortadas + 1;
            int ultimo = Math.Min(tarjetas.Count, inicioPaginaPortadas + PortadasPorPagina);
            tituloPortadas.Text = tarjetas.Count == 0 ? "PORTADAS" : "PORTADAS " + primero + "–" + ultimo + " DE " + tarjetas.Count;
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
            foreach (ListViewItem elemento in todosLosElementos)
            {
                string tipo = Convert.ToString(elemento.ToolTipText).Trim();
                string genero = Convert.ToString(elemento.Name).Trim();
                if (!String.IsNullOrWhiteSpace(tipo) && !nombresGenero.Exists(delegate(string n) { return IgualNormal(n, tipo); }))
                    nombresGenero.Add(tipo);
                if (!String.IsNullOrWhiteSpace(genero) && !nombresGenero.Exists(delegate(string n) { return IgualNormal(n, genero); }))
                    nombresGenero.Add(genero);
            }
            for (int i = botonesTipo.Count - 1; i >= 0; i--)
            {
                Button viejo = botonesTipo[i].Key;
                if (viejo.Parent == listaEstilos)
                {
                    viejo.Parent.Controls.Remove(viejo);
                    viejo.Dispose();
                    botonesTipo.RemoveAt(i);
                }
            }
            foreach (string nombre in nombresGenero)
            {
                Button estilo = CrearBotonGenero(nombre, 142, 42);
                estilo.Margin = new Padding(4, 2, 4, 2);
                estilo.FlatAppearance.BorderSize = 1;
                estilo.FlatAppearance.BorderColor = colorInterfaz;
                listaEstilos.Controls.Add(estilo);
            }
            if (!String.Equals(filtroTipo, "Todos", StringComparison.OrdinalIgnoreCase) &&
                !ListaModos().Exists(delegate(string v) { return IgualNormal(v, filtroTipo); }))
                filtroTipo = "Todos";
            AplicarVisibilidadGeneros();
            ResaltarTipos();
        }

        private void AplicarVisibilidadGeneros()
        {
            if (listaEstilos != null)
                listaEstilos.Visible = ConfiguracionTeclado.MostrarListaGeneros && listaEstilos.Controls.Count > 0;
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

        private void AlternarListaReproduccion()
        {
            if (panelCola == null || panelPublicidad == null) return;
            panelCola.Visible = !panelCola.Visible;
            if (panelCola.Visible) { panelCola.BringToFront(); cola.Focus(); }
            else { panelPublicidad.Visible = true; panelPublicidad.BringToFront(); }
        }

        private bool EsArchivoVideo(string ruta)
        {
            string extension = Path.GetExtension(ruta ?? "").ToLowerInvariant();
            return extension == ".mp4" || extension == ".mkv" || extension == ".avi" ||
                extension == ".mov" || extension == ".wmv" || extension == ".webm" ||
                extension == ".mpeg" || extension == ".mpg" || extension == ".m4v" ||
                extension == ".ts" || extension == ".ogv" || extension == ".3gp";
        }

        private string ArchivoColorInterfaz
        {
            get { return Path.Combine(Application.UserAppDataPath, "color-interfaz.txt"); }
        }

        private void CargarColorInterfaz()
        {
            try { if (File.Exists(ArchivoColorInterfaz)) colorInterfaz = ColorTranslator.FromHtml(File.ReadAllText(ArchivoColorInterfaz).Trim()); }
            catch { colorInterfaz = Color.FromArgb(0, 220, 240); }
        }

        private void CambiarColorInterfaz()
        {
            using (ColorDialog selector = new ColorDialog { Color = colorInterfaz, FullOpen = true })
            {
                if (selector.ShowDialog(this) != DialogResult.OK) return;
                colorInterfaz = selector.Color;
                try
                {
                    string carpeta = Path.GetDirectoryName(ArchivoColorInterfaz);
                    if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);
                    File.WriteAllText(ArchivoColorInterfaz, ColorTranslator.ToHtml(colorInterfaz));
                }
                catch (Exception ex) { MessageBox.Show(this, "No se pudo guardar el color: " + ex.Message, "Color de interfaz", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                AplicarTemaInterfaz();
            }
        }

        private void AplicarTemaInterfaz()
        {
            if (tituloPortadas != null) tituloPortadas.ForeColor = colorInterfaz;
            if (etiquetaCola != null) etiquetaCola.ForeColor = colorInterfaz;
            if (letreroLed != null) letreroLed.ForeColor = colorInterfaz;
            foreach (KeyValuePair<Button, string> par in botonesTipo)
                if (par.Key != null && !par.Key.IsDisposed) par.Key.FlatAppearance.BorderColor = colorInterfaz;
            AplicarColorAControles(this);
            foreach (Panel tarjeta in tarjetas) tarjeta.Invalidate();
            Invalidate(true);
        }

        private void AplicarColorAControles(Control raiz)
        {
            foreach (Control control in raiz.Controls)
            {
                Button boton = control as Button;
                if (boton != null) boton.FlatAppearance.BorderColor = colorInterfaz;
                if (control is Panel) control.Invalidate();
                AplicarColorAControles(control);
            }
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
            foreach (char letra in "ABCDEFGHIJKLMNÑOPQRSTUVWXYZ0123456789") teclas.Add(letra.ToString());
            teclas.AddRange(new string[] { "ESPACIO", "-", "_", ".", ",", "'", "&", "+", "BORRAR", "LIMPIAR", "CERRAR" });
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
            barraAbc.BringToFront();
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
            else if (tecla == "LIMPIAR") ActualizarTextoBusqueda("");
            else ActualizarTextoBusqueda(actual + (tecla.Length == 1 ? tecla.ToLowerInvariant() : tecla));
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
                case "COLOR": CambiarColorInterfaz(); break;
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
            reloj.Dispose();
            temporizadorAvisoAgregar.Stop();
            temporizadorAvisoAgregar.Dispose();
if (temporizadorAbecedario != null) { temporizadorAbecedario.Stop(); temporizadorAbecedario.Dispose(); temporizadorAbecedario = null; }
            if (reproductor != null) { reproductor.Stop(); reproductor.Dispose(); reproductor = null; }
            if (medioActual != null) { medioActual.Dispose(); medioActual = null; }
            if (motor != null) { motor.Dispose(); motor = null; }
            if (pictureBoxPortada != null && pictureBoxPortada.Image != null)
            {
                pictureBoxPortada.Image.Dispose();
                pictureBoxPortada.Image = null;
            }
            lock (candadoImagenCajaCD)
            {
                if (imagenBaseCajaCD != null)
                {
                    imagenBaseCajaCD.Dispose();
                    imagenBaseCajaCD = null;
                }
            }
            foreach (Control tarjeta in mosaico.Controls)
            {
                foreach (Control hijo in tarjeta.Controls)
                {
                    PictureBox cuadro = hijo as PictureBox;
                    if (cuadro != null && cuadro.Image != null)
                    {
                        cuadro.Image.Dispose();
                        cuadro.Image = null;
                    }
                }
            }
        }
    }
}
