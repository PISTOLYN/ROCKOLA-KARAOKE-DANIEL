using System;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace RockolaKaraoke.Servicios
{
    public static class ConfiguracionSkin
    {
        static string Leer(string k, string d)
        {
            try
            {
                using (var c = Datos.BaseDatos.CrearConexion())
                using (var q = new SQLiteCommand("SELECT Valor FROM Configuracion WHERE Clave=@k", c))
                {
                    c.Open();
                    q.Parameters.AddWithValue("@k", k);
                    object v = q.ExecuteScalar();
                    return v == null ? d : Convert.ToString(v);
                }
            }
            catch { return d; }
        }

        static void Escribir(string k, string v)
        {
            using (var c = Datos.BaseDatos.CrearConexion())
            using (var q = new SQLiteCommand("INSERT OR REPLACE INTO Configuracion(Clave,Valor) VALUES(@k,@v)", c))
            {
                c.Open();
                q.Parameters.AddWithValue("@k", k);
                q.Parameters.AddWithValue("@v", v ?? "");
                q.ExecuteNonQuery();
            }
        }

        static Color ColorValor(string k, Color d)
        {
            int n;
            return int.TryParse(Leer(k, d.ToArgb().ToString()), out n) ? Color.FromArgb(n) : d;
        }

        public static Color Fondo { get { return ColorValor("Skin.Fondo", Color.FromArgb(12, 16, 31)); } }
        public static Color Panel { get { return ColorValor("Skin.Panel", Color.FromArgb(23, 30, 53)); } }
        public static Color Acento { get { return ColorValor("Skin.Acento", Color.FromArgb(30, 105, 170)); } }
        public static Color Texto { get { return ColorValor("Skin.Texto", Color.White); } }
        
        public static string Imagen { get { return Leer("Skin.Imagen", ""); } }
        public static string Resolucion { get { return Leer("Skin.Resolucion", "Pantalla completa"); } }
        public static string Mensajes { get { return Leer("Skin.Mensajes", "¡Elige tu música favorita!|Disfruta de la mejor música y videos"); } }
        
        public static int TamanoTitulo
        {
            get
            {
                int n;
                return int.TryParse(Leer("Skin.TamanoTitulo", "28"), out n) ? Math.Max(16, Math.Min(48, n)) : 28;
            }
        }

        public static string SkinActivo { get { return Leer("Skin.Activo", "Default"); } }
        public static void GuardarSkinActivo(string nombreSkin) { Escribir("Skin.Activo", nombreSkin); }

        public static void GuardarTodo(Color f, Color p, Color a, Color t, string img, string res, string msg, int tam)
        {
            Escribir("Skin.Fondo", f.ToArgb().ToString());
            Escribir("Skin.Panel", p.ToArgb().ToString());
            Escribir("Skin.Acento", a.ToArgb().ToString());
            Escribir("Skin.Texto", t.ToArgb().ToString());
            Escribir("Skin.Imagen", img);
            Escribir("Skin.Resolucion", res);
            Escribir("Skin.Mensajes", msg);
            Escribir("Skin.TamanoTitulo", tam.ToString());
        }

        public static void Aplicar(Control raiz)
        {
            raiz.BackColor = Fondo;
            raiz.ForeColor = Texto;

            bool tieneImagen = false;
            string rutaFinal = Imagen;
            
            if (String.IsNullOrWhiteSpace(rutaFinal))
            {
                string carpetaSkin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skins", SkinActivo);
                rutaFinal = Path.Combine(carpetaSkin, "FondoPrincipal.jpg");
            }

            if (!String.IsNullOrWhiteSpace(rutaFinal) && File.Exists(rutaFinal))
            {
                try 
                { 
                    using (Image i = Image.FromFile(rutaFinal)) 
                    { 
                        raiz.BackgroundImage = new Bitmap(i); 
                        // Usamos Stretch para que llene la pantalla, o Zoom si prefieres que no se deforme
                        raiz.BackgroundImageLayout = ImageLayout.Stretch; 
                        tieneImagen = true;
                    } 
                } 
                catch { }
            }

            if (!tieneImagen) raiz.BackgroundImage = null;

            Hijos(raiz, tieneImagen);

            Form f = raiz as Form;
            if (f != null)
            {
                if (Resolucion == "Pantalla completa") 
                    f.WindowState = FormWindowState.Maximized;
                else
                {
                    string[] p = Resolucion.Split('x');
                    int w, h;
                    if (p.Length == 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h))
                    {
                        f.WindowState = FormWindowState.Normal;
                        f.Size = new Size(w, h);
                        f.StartPosition = FormStartPosition.CenterScreen;
                    }
                }
            }
        }

        static void Hijos(Control p, bool tieneImagen)
        {
            foreach (Control c in p.Controls)
            {
                if (c is Button)
                {
                    c.BackColor = Acento;
                    c.ForeColor = Texto;
                    Button b = (Button)c;
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderSize = 0;
                }
                else if (c is ListView || c is ListBox)
                {
                    c.BackColor = Panel;
                    c.ForeColor = Texto;
                }
                else if (c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel)
                {
                    // TRUCO DE TRANSPARENCIA: Si hay imagen, usamos un color oscuro semitransparente
                    if (tieneImagen)
                        c.BackColor = Color.FromArgb(200, Panel.R, Panel.G, Panel.B); // 200 de 255 es semitransparente
                    else
                        c.BackColor = Panel;
                }
                else 
                {
                    c.ForeColor = Texto;
                }
                
                Hijos(c, tieneImagen);
            }
        }
    }
}
