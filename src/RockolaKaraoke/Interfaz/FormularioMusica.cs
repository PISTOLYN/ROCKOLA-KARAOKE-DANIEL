using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RockolaKaraoke.Servicios;

namespace RockolaKaraoke.Interfaz
{
    /// <summary>Pide un texto en una ventana pequeña (nombre de género, alias...).</summary>
    public static class DialogoTexto
    {
        public static string Pedir(IWin32Window dueno, string titulo, string inicial)
        {
            using (Form f = new Form())
            {
                f.Text = titulo;
                f.StartPosition = FormStartPosition.CenterParent;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
                f.ClientSize = new Size(560, 150);
                f.BackColor = Color.FromArgb(5, 14, 32);
                f.ForeColor = Color.White;
                Label etiqueta = new Label { Text = titulo, Left = 15, Top = 12, Width = 530, Height = 28, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.Gold };
                TextBox caja = new TextBox { Left = 15, Top = 48, Width = 530, Font = new Font("Segoe UI", 14F), Text = inicial ?? "" };
                Button aceptar = new Button { Text = "ACEPTAR", Left = 15, Top = 95, Width = 250, Height = 40, DialogResult = DialogResult.OK };
                Button cancelar = new Button { Text = "CANCELAR", Left = 295, Top = 95, Width = 250, Height = 40, DialogResult = DialogResult.Cancel };
                foreach (Button b in new Button[] { aceptar, cancelar })
                {
                    b.FlatStyle = FlatStyle.Flat; b.BackColor = Color.FromArgb(0, 105, 175); b.ForeColor = Color.White;
                    b.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                }
                f.AcceptButton = aceptar; f.CancelButton = cancelar;
                f.Controls.Add(etiqueta); f.Controls.Add(caja); f.Controls.Add(aceptar); f.Controls.Add(cancelar);
                f.Shown += delegate { caja.SelectAll(); caja.Focus(); };
                if (f.ShowDialog(dueno) != DialogResult.OK) return null;
                string resultado = caja.Text.Trim();
                return resultado.Length == 0 ? null : resultado;
            }
        }
    }

    /// <summary>CONFIGURAR MÚSICA: géneros y carpetas (artistas) de cada género.</summary>
    public class FormularioMusica : Form
    {
        private readonly ListBox listaGeneros = new ListBox();
        private readonly ListView listaCarpetas = new ListView();
        private readonly Label etiquetaGeneros = new Label();
        private readonly Label etiquetaCarpetas = new Label();
        private readonly Label estado = new Label();
        private List<GeneroInfo> generos = new List<GeneroInfo>();
        private List<CarpetaGenero> carpetas = new List<CarpetaGenero>();

        public FormularioMusica()
        {
            Text = "CONFIGURAR MÚSICA - GÉNEROS Y ARTISTAS";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 760);
            MinimumSize = new Size(900, 700);
            BackColor = Color.FromArgb(5, 14, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            Label titulo = new Label { Text = "GÉNEROS - ARTISTAS", Dock = DockStyle.Top, Height = 46, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(0, 235, 245), Font = new Font("Segoe UI", 16F, FontStyle.Bold) };
            Label instrucciones = new Label
            {
                Text = "1. CREE UN NUEVO GÉNERO MUSICAL (por ejemplo: KARAOKE, MÚSICA, VIDEO).\r\n" +
                       "2. AGREGUE CARPETAS AL GÉNERO CREADO. CADA CARPETA REPRESENTA A UN ARTISTA, Y DENTRO DE DICHA CARPETA DEBEN ENCONTRARSE LOS TEMAS.\r\n" +
                       "3. HAGA CLIC EN \"ACTUALIZAR\" CUANDO AGREGUE NUEVA MÚSICA O VIDEOS A SUS CARPETAS.",
                Dock = DockStyle.Bottom, Height = 84, BackColor = Color.FromArgb(255, 235, 160), ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(8, 4, 8, 4)
            };
            estado.Dock = DockStyle.Bottom; estado.Height = 26; estado.TextAlign = ContentAlignment.MiddleLeft; estado.ForeColor = Color.LightGray;
            estado.Padding = new Padding(8, 0, 0, 0);

            TableLayoutPanel cuerpo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(6) };
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cuerpo.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));
            cuerpo.RowStyles.Add(new RowStyle(SizeType.Percent, 66F));

            // --- Géneros ---
            Panel grupoGeneros = new Panel { Dock = DockStyle.Fill };
            listaGeneros.Dock = DockStyle.Fill; listaGeneros.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            listaGeneros.BackColor = Color.FromArgb(235, 240, 245); listaGeneros.ForeColor = Color.Black;
            listaGeneros.SelectedIndexChanged += delegate { CargarCarpetas(); };
            FlowLayoutPanel botonesGeneros = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 200, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            Button agregarGenero = CrearBoton("AGREGAR GÉNERO", botonesGeneros);
            Button borrarGenero = CrearBoton("BORRAR GÉNERO", botonesGeneros);
            Button modificarGenero = CrearBoton("MODIFICAR NOMBRE", botonesGeneros);
            Button actualizarGenero = CrearBoton("ACTUALIZAR", botonesGeneros);
            etiquetaGeneros.Dock = DockStyle.Top; etiquetaGeneros.Height = 28; etiquetaGeneros.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            etiquetaGeneros.ForeColor = Color.FromArgb(0, 235, 245); etiquetaGeneros.TextAlign = ContentAlignment.MiddleLeft;
            grupoGeneros.Controls.Add(listaGeneros); grupoGeneros.Controls.Add(botonesGeneros); grupoGeneros.Controls.Add(etiquetaGeneros);

            // --- Carpetas (artistas) ---
            Panel grupoCarpetas = new Panel { Dock = DockStyle.Fill };
            listaCarpetas.Dock = DockStyle.Fill; listaCarpetas.View = View.Details; listaCarpetas.FullRowSelect = true; listaCarpetas.HideSelection = false;
            listaCarpetas.MultiSelect = false; listaCarpetas.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            listaCarpetas.BackColor = Color.FromArgb(235, 240, 245); listaCarpetas.ForeColor = Color.Black;
            listaCarpetas.Columns.Add("ALIAS (nombre que se muestra)", 300); listaCarpetas.Columns.Add("CARPETA", 560);
            listaCarpetas.ItemActivate += delegate { ModificarAlias(); };
            FlowLayoutPanel botonesCarpetas = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 200, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            Button agregarCarpeta = CrearBoton("AGREGAR CARPETA", botonesCarpetas);
            Button agregarSub = CrearBoton("AGREGAR SUB-CARPETAS", botonesCarpetas);
            Button borrarCarpeta = CrearBoton("BORRAR", botonesCarpetas);
            Button borrarTodo = CrearBoton("BORRAR TODO", botonesCarpetas);
            Button modificarAlias = CrearBoton("MODIFICAR ALIAS", botonesCarpetas);
            Button explorador = CrearBoton("EXPLORADOR DE WINDOWS", botonesCarpetas);
            Button actualizarCarpetas = CrearBoton("ACTUALIZAR (RE-ESCANEAR)", botonesCarpetas);
            etiquetaCarpetas.Dock = DockStyle.Top; etiquetaCarpetas.Height = 28; etiquetaCarpetas.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            etiquetaCarpetas.ForeColor = Color.FromArgb(0, 235, 245); etiquetaCarpetas.TextAlign = ContentAlignment.MiddleLeft;
            grupoCarpetas.Controls.Add(listaCarpetas); grupoCarpetas.Controls.Add(botonesCarpetas); grupoCarpetas.Controls.Add(etiquetaCarpetas);

            cuerpo.Controls.Add(grupoGeneros, 0, 0);
            cuerpo.Controls.Add(grupoCarpetas, 0, 1);
            Controls.Add(cuerpo); Controls.Add(instrucciones); Controls.Add(estado); Controls.Add(titulo);

            agregarGenero.Click += delegate { AgregarGenero(); };
            borrarGenero.Click += delegate { BorrarGenero(); };
            modificarGenero.Click += delegate { ModificarGenero(); };
            actualizarGenero.Click += delegate { Reescanear(); };
            agregarCarpeta.Click += delegate { AgregarCarpeta(false); };
            agregarSub.Click += delegate { AgregarCarpeta(true); };
            borrarCarpeta.Click += delegate { BorrarCarpeta(); };
            borrarTodo.Click += delegate { BorrarTodo(); };
            modificarAlias.Click += delegate { ModificarAlias(); };
            explorador.Click += delegate { AbrirExplorador(); };
            actualizarCarpetas.Click += delegate { Reescanear(); };

            Load += delegate { CargarGeneros(-1); };
        }

        private static Button CrearBoton(string texto, Control padre)
        {
            Button b = new Button { Text = texto, Width = 190, Height = 36, Margin = new Padding(3, 2, 3, 2) };
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(0, 105, 175); b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 9F, FontStyle.Bold); b.Cursor = Cursors.Hand;
            padre.Controls.Add(b);
            return b;
        }

        private GeneroInfo GeneroActual
        {
            get { return listaGeneros.SelectedIndex >= 0 && listaGeneros.SelectedIndex < generos.Count ? generos[listaGeneros.SelectedIndex] : null; }
        }

        private CarpetaGenero CarpetaActual
        {
            get { return listaCarpetas.SelectedIndices.Count > 0 ? carpetas[listaCarpetas.SelectedIndices[0]] : null; }
        }

        private void CargarGeneros(long seleccionarId)
        {
            long previo = seleccionarId >= 0 ? seleccionarId : (GeneroActual != null ? GeneroActual.Id : -1);
            generos = ServicioGeneros.ListarGeneros();
            listaGeneros.Items.Clear();
            int indice = generos.Count > 0 ? 0 : -1;
            for (int i = 0; i < generos.Count; i++)
            {
                listaGeneros.Items.Add(generos[i].Nombre);
                if (generos[i].Id == previo) indice = i;
            }
            etiquetaGeneros.Text = "GÉNEROS MUSICALES (" + generos.Count + ")";
            if (indice >= 0) listaGeneros.SelectedIndex = indice; else CargarCarpetas();
        }

        private void CargarCarpetas()
        {
            listaCarpetas.Items.Clear();
            GeneroInfo g = GeneroActual;
            carpetas = g == null ? new List<CarpetaGenero>() : ServicioGeneros.ListarCarpetas(g.Id);
            foreach (CarpetaGenero c in carpetas)
            {
                ListViewItem fila = new ListViewItem(c.Alias);
                fila.SubItems.Add(c.Carpeta);
                listaCarpetas.Items.Add(fila);
            }
            etiquetaCarpetas.Text = "ARTISTAS / CARPETAS DE " + (g == null ? "(elige un género)" : g.Nombre.ToUpperInvariant()) + " (" + carpetas.Count + ")";
            if (listaCarpetas.Items.Count > 0) listaCarpetas.Items[0].Selected = true;
        }

        private void AgregarGenero()
        {
            string nombre = DialogoTexto.Pedir(this, "INGRESE EL NOMBRE DEL GÉNERO MUSICAL", "");
            if (nombre == null) return;
            try { CargarGeneros(ServicioGeneros.AgregarGenero(nombre)); estado.Text = "Género creado: " + nombre; }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo crear el género.\n" + ex.Message, "Géneros", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void ModificarGenero()
        {
            GeneroInfo g = GeneroActual;
            if (g == null) return;
            string nombre = DialogoTexto.Pedir(this, "NUEVO NOMBRE DEL GÉNERO", g.Nombre);
            if (nombre == null) return;
            try { ServicioGeneros.RenombrarGenero(g.Id, nombre); CargarGeneros(g.Id); }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo cambiar el nombre (¿ya existe otro igual?).\n" + ex.Message, "Géneros", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void BorrarGenero()
        {
            GeneroInfo g = GeneroActual;
            if (g == null) return;
            if (MessageBox.Show(this, "¿Borrar el género \"" + g.Nombre + "\" y quitar sus carpetas de la rockola?\n(No se borra ningún archivo del disco.)", "Borrar género", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            ServicioGeneros.BorrarGenero(g.Id);
            CargarGeneros(-1);
        }

        private void AgregarCarpeta(bool subcarpetas)
        {
            GeneroInfo g = GeneroActual;
            if (g == null) { MessageBox.Show(this, "Primero crea o elige un género.", "Carpetas", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (FolderBrowserDialog dialogo = new FolderBrowserDialog())
            {
                dialogo.Description = subcarpetas ? "Elige la carpeta que contiene las carpetas de cada artista." : "Elige la carpeta del artista (con sus temas dentro).";
                dialogo.ShowNewFolderButton = false;
                if (dialogo.ShowDialog(this) != DialogResult.OK) return;
                List<string> aEscanear = new List<string>();
                int nuevas = 0;
                try
                {
                    if (subcarpetas)
                    {
                        string[] hijas = Directory.GetDirectories(dialogo.SelectedPath);
                        Array.Sort(hijas, StringComparer.CurrentCultureIgnoreCase);
                        foreach (string hija in hijas)
                            if (ServicioGeneros.AgregarCarpeta(g.Id, NombreCarpeta(hija), hija)) { nuevas++; aEscanear.Add(hija); }
                    }
                    else if (ServicioGeneros.AgregarCarpeta(g.Id, NombreCarpeta(dialogo.SelectedPath), dialogo.SelectedPath))
                    {
                        nuevas++; aEscanear.Add(dialogo.SelectedPath);
                    }
                }
                catch (Exception ex) { MessageBox.Show(this, "No se pudo agregar.\n" + ex.Message, "Carpetas", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (nuevas == 0) { MessageBox.Show(this, "Esa carpeta ya estaba en el género (o no tiene sub-carpetas).", "Carpetas", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                EscanearCarpetas(aEscanear, g.Id);
                CargarCarpetas();
            }
        }

        private static string NombreCarpeta(string ruta)
        {
            string nombre = Path.GetFileName(ruta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return String.IsNullOrWhiteSpace(nombre) ? ruta : nombre;
        }

        private void EscanearCarpetas(IEnumerable<string> lista, long generoId)
        {
            Cursor = Cursors.WaitCursor;
            estado.Text = "Escaneando música y videos... espera un momento.";
            Application.DoEvents();
            try
            {
                ResultadoEscaneo r = ServicioGeneros.Escanear(lista, generoId);
                estado.Text = "Escaneo terminado. Archivos: " + r.ArchivosEncontrados + "  Nuevos: " + r.Nuevos + "  Actualizados: " + r.Actualizados + "  Errores: " + r.Errores;
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo completar el escaneo.\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { Cursor = Cursors.Default; }
        }

        private void Reescanear()
        {
            GeneroInfo g = GeneroActual;
            if (g == null || carpetas.Count == 0) return;
            List<string> rutas = new List<string>();
            foreach (CarpetaGenero c in carpetas) rutas.Add(c.Carpeta);
            EscanearCarpetas(rutas, g.Id);
        }

        private void BorrarCarpeta()
        {
            CarpetaGenero c = CarpetaActual;
            if (c == null) return;
            if (MessageBox.Show(this, "¿Quitar \"" + c.Alias + "\" de este género?\n(No se borra ningún archivo del disco.)", "Borrar carpeta", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            ServicioGeneros.BorrarCarpeta(c);
            CargarCarpetas();
        }

        private void BorrarTodo()
        {
            GeneroInfo g = GeneroActual;
            if (g == null || carpetas.Count == 0) return;
            if (MessageBox.Show(this, "¿Quitar TODAS las carpetas del género \"" + g.Nombre + "\"?\n(No se borra ningún archivo del disco.)", "Borrar todo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            ServicioGeneros.BorrarTodasLasCarpetas(g.Id);
            CargarCarpetas();
        }

        private void ModificarAlias()
        {
            CarpetaGenero c = CarpetaActual;
            if (c == null) return;
            string alias = DialogoTexto.Pedir(this, "ALIAS (NOMBRE QUE SE MUESTRA EN LA MINIATURA)", c.Alias);
            if (alias == null) return;
            ServicioGeneros.ModificarAlias(c.Id, alias);
            int indice = listaCarpetas.SelectedIndices.Count > 0 ? listaCarpetas.SelectedIndices[0] : 0;
            CargarCarpetas();
            if (indice < listaCarpetas.Items.Count) listaCarpetas.Items[indice].Selected = true;
        }

        private void AbrirExplorador()
        {
            CarpetaGenero c = CarpetaActual;
            if (c == null) return;
            try { Process.Start("explorer.exe", "\"" + c.Carpeta + "\""); }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo abrir el explorador.\n" + ex.Message); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
