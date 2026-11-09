using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;

namespace RockolaKaraoke.Interfaz
{
    public class FormularioBiblioteca : Form
    {
        private readonly DataGridView tabla = new DataGridView();
        private readonly TextBox buscar = new TextBox();
        private readonly Label estado = new Label();
        private readonly Button escanear = new Button();

        public FormularioBiblioteca()
        {
            Text = "Administrador de biblioteca";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(900, 600);
            BackColor = Color.FromArgb(15, 18, 35);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            Panel barra = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.FromArgb(24, 29, 52) };
            Label titulo = new Label { Text = "BIBLIOTECA MUSICAL", Location = new Point(15, 18), Size = new Size(280, 45), ForeColor = Color.DeepSkyBlue, Font = new Font("Segoe UI", 17F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            buscar.Location = new Point(310, 22); buscar.Size = new Size(240, 32); buscar.Font = new Font("Segoe UI", 11F);
            buscar.TextChanged += delegate { CargarCanciones(); };
            escanear.Text = "ESCANEAR CARPETAS"; escanear.Location = new Point(570, 16); escanear.Size = new Size(190, 44);
            Estilizar(escanear); escanear.Click += Escanear_Click;
            Button actualizar = new Button { Text = "ACTUALIZAR", Location = new Point(775, 16), Size = new Size(130, 44) };
            Estilizar(actualizar); actualizar.Click += delegate { CargarCanciones(); };
            barra.Controls.Add(titulo); barra.Controls.Add(buscar); barra.Controls.Add(escanear); barra.Controls.Add(actualizar);

            tabla.Dock = DockStyle.Fill; tabla.ReadOnly = true; tabla.AllowUserToAddRows = false; tabla.AllowUserToDeleteRows = false;
            tabla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; tabla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            tabla.MultiSelect = false; tabla.BackgroundColor = Color.FromArgb(20, 24, 43); tabla.BorderStyle = BorderStyle.None;
            tabla.EnableHeadersVisualStyles = false; tabla.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 90, 150);
            tabla.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; tabla.DefaultCellStyle.BackColor = Color.FromArgb(25, 30, 50);
            tabla.DefaultCellStyle.ForeColor = Color.White; tabla.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 100, 160);
            tabla.DefaultCellStyle.SelectionForeColor = Color.White; tabla.RowHeadersVisible = false;

            estado.Dock = DockStyle.Bottom; estado.Height = 38; estado.TextAlign = ContentAlignment.MiddleLeft;
            estado.Padding = new Padding(12, 0, 0, 0); estado.ForeColor = Color.LightGray;
            Controls.Add(tabla); Controls.Add(estado); Controls.Add(barra);
            Load += delegate { CargarCanciones(); };
        }

        private static void Estilizar(Button boton)
        {
            boton.FlatStyle = FlatStyle.Flat; boton.FlatAppearance.BorderSize = 0;
            boton.BackColor = Color.FromArgb(35, 90, 150); boton.ForeColor = Color.White;
            boton.Font = new Font("Segoe UI", 9F, FontStyle.Bold); boton.Cursor = Cursors.Hand;
        }

        private void Escanear_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialogo = new FolderBrowserDialog())
            {
                dialogo.Description = "Selecciona la carpeta principal de música y videos. Se revisarán sus subcarpetas.";
                dialogo.ShowNewFolderButton = false;
                if (dialogo.ShowDialog(this) != DialogResult.OK) return;
                escanear.Enabled = false; Cursor = Cursors.WaitCursor;
                try
                {
                    Servicios.ResultadoEscaneo r = Servicios.EscanerBiblioteca.Escanear(new string[] { dialogo.SelectedPath });
                    CargarCanciones();
                    MessageBox.Show(this, "Escaneo terminado.\nArchivos encontrados: " + r.ArchivosEncontrados +
                        "\nNuevos: " + r.Nuevos + "\nActualizados: " + r.Actualizados +
                        "\nErrores: " + r.Errores, "Biblioteca", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "No se pudo completar el escaneo.\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { Cursor = Cursors.Default; escanear.Enabled = true; }
            }
        }

        private void CargarCanciones()
        {
            try
            {
                using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
                using (SQLiteDataAdapter adaptador = new SQLiteDataAdapter(
                    "SELECT c.Id, c.Titulo AS Cancion, IFNULL(a.Nombre,'Sin artista') AS Artista, c.Tipo, c.RutaArchivo AS Ubicacion " +
                    "FROM Canciones c LEFT JOIN Artistas a ON a.Id=c.ArtistaId " +
                    "WHERE c.Activa=1 AND (@filtro='%%' OR c.Titulo LIKE @filtro OR a.Nombre LIKE @filtro) ORDER BY a.Nombre,c.Titulo;", conexion))
                {
                    adaptador.SelectCommand.Parameters.AddWithValue("@filtro", "%" + buscar.Text.Trim() + "%");
                    DataTable datos = new DataTable(); adaptador.Fill(datos); tabla.DataSource = datos;
                    if (tabla.Columns.Contains("Id")) tabla.Columns["Id"].Visible = false;
                    estado.Text = "Elementos registrados: " + datos.Rows.Count;
                }
            }
            catch (Exception ex) { estado.Text = "No se pudo cargar la biblioteca: " + ex.Message; }
        }
    }
}