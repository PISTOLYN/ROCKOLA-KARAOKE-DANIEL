using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace RockolaKaraoke.Servicios
{
    public class GeneroInfo
    {
        public long Id;
        public string Nombre;
    }

    public class CarpetaGenero
    {
        public long Id;
        public long GeneroId;
        public string Alias;
        public string Carpeta;
    }

    /// <summary>Géneros musicales y las carpetas (artistas) que contiene cada uno.</summary>
    public static class ServicioGeneros
    {
        private static void Ejecutar(string sql, params object[] parametros)
        {
            using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
            using (SQLiteCommand comando = new SQLiteCommand(sql, conexion))
            {
                for (int i = 0; i + 1 < parametros.Length; i += 2)
                    comando.Parameters.AddWithValue(Convert.ToString(parametros[i]), parametros[i + 1]);
                conexion.Open();
                comando.ExecuteNonQuery();
            }
        }

        public static List<GeneroInfo> ListarGeneros()
        {
            List<GeneroInfo> lista = new List<GeneroInfo>();
            using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
            using (SQLiteCommand comando = new SQLiteCommand("SELECT Id, Nombre FROM Generos ORDER BY Id;", conexion))
            {
                conexion.Open();
                using (SQLiteDataReader lector = comando.ExecuteReader())
                    while (lector.Read())
                        lista.Add(new GeneroInfo { Id = Convert.ToInt64(lector[0]), Nombre = Convert.ToString(lector[1]) });
            }
            return lista;
        }

        public static long AgregarGenero(string nombre)
        {
            using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
            using (SQLiteCommand comando = new SQLiteCommand("INSERT OR IGNORE INTO Generos(Nombre) VALUES(@n); SELECT Id FROM Generos WHERE Nombre=@n COLLATE NOCASE;", conexion))
            {
                comando.Parameters.AddWithValue("@n", nombre);
                conexion.Open();
                return Convert.ToInt64(comando.ExecuteScalar());
            }
        }

        public static void RenombrarGenero(long id, string nombre)
        {
            Ejecutar("UPDATE Generos SET Nombre=@n WHERE Id=@id;", "@n", nombre, "@id", id);
        }

        public static void BorrarGenero(long id)
        {
            using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
            {
                conexion.Open();
                using (SQLiteTransaction t = conexion.BeginTransaction())
                {
                    foreach (string sql in new string[] {
                        "UPDATE Canciones SET Activa=0, GeneroId=NULL WHERE GeneroId=@id;",
                        "DELETE FROM GeneroCarpetas WHERE GeneroId=@id;",
                        "DELETE FROM Generos WHERE Id=@id;" })
                    {
                        using (SQLiteCommand c = new SQLiteCommand(sql, conexion, t))
                        {
                            c.Parameters.AddWithValue("@id", id);
                            c.ExecuteNonQuery();
                        }
                    }
                    t.Commit();
                }
            }
        }

        public static List<CarpetaGenero> ListarCarpetas(long generoId)
        {
            List<CarpetaGenero> lista = new List<CarpetaGenero>();
            using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
            using (SQLiteCommand comando = new SQLiteCommand("SELECT Id, GeneroId, Alias, Carpeta FROM GeneroCarpetas WHERE GeneroId=@g ORDER BY Alias COLLATE NOCASE;", conexion))
            {
                comando.Parameters.AddWithValue("@g", generoId);
                conexion.Open();
                using (SQLiteDataReader lector = comando.ExecuteReader())
                    while (lector.Read())
                        lista.Add(new CarpetaGenero { Id = Convert.ToInt64(lector[0]), GeneroId = Convert.ToInt64(lector[1]), Alias = Convert.ToString(lector[2]), Carpeta = Convert.ToString(lector[3]) });
            }
            return lista;
        }

        /// <summary>Devuelve false si esa carpeta ya estaba en el género.</summary>
        public static bool AgregarCarpeta(long generoId, string alias, string carpeta)
        {
            using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
            using (SQLiteCommand comando = new SQLiteCommand("INSERT OR IGNORE INTO GeneroCarpetas(GeneroId,Alias,Carpeta) VALUES(@g,@a,@c);", conexion))
            {
                comando.Parameters.AddWithValue("@g", generoId);
                comando.Parameters.AddWithValue("@a", alias);
                comando.Parameters.AddWithValue("@c", carpeta);
                conexion.Open();
                return comando.ExecuteNonQuery() > 0;
            }
        }

        public static void ModificarAlias(long id, string alias)
        {
            Ejecutar("UPDATE GeneroCarpetas SET Alias=@a WHERE Id=@id;", "@a", alias, "@id", id);
        }

        private static string ConSeparador(string carpeta)
        {
            return carpeta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        /// <summary>Quita la carpeta del género y oculta sus canciones.</summary>
        public static void BorrarCarpeta(CarpetaGenero carpeta)
        {
            string prefijo = ConSeparador(carpeta.Carpeta);
            Ejecutar("UPDATE Canciones SET Activa=0 WHERE GeneroId=@g AND SUBSTR(RutaArchivo,1,LENGTH(@c))=@c COLLATE NOCASE;", "@g", carpeta.GeneroId, "@c", prefijo);
            Ejecutar("DELETE FROM GeneroCarpetas WHERE Id=@id;", "@id", carpeta.Id);
        }

        public static void BorrarTodasLasCarpetas(long generoId)
        {
            Ejecutar("UPDATE Canciones SET Activa=0 WHERE GeneroId=@g;", "@g", generoId);
            Ejecutar("DELETE FROM GeneroCarpetas WHERE GeneroId=@g;", "@g", generoId);
        }

        /// <summary>Alias que se muestran en las miniaturas, por carpeta.</summary>
        public static Dictionary<string, string> AliasPorCarpeta()
        {
            Dictionary<string, string> mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
            using (SQLiteCommand comando = new SQLiteCommand("SELECT Carpeta, Alias FROM GeneroCarpetas ORDER BY Id;", conexion))
            {
                conexion.Open();
                using (SQLiteDataReader lector = comando.ExecuteReader())
                    while (lector.Read())
                    {
                        string clave = Convert.ToString(lector[0]).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (!mapa.ContainsKey(clave)) mapa.Add(clave, Convert.ToString(lector[1]));
                    }
            }
            return mapa;
        }

        public static ResultadoEscaneo Escanear(IEnumerable<string> carpetas, long generoId)
        {
            return EscanerBiblioteca.Escanear(carpetas, generoId);
        }
    }
}
