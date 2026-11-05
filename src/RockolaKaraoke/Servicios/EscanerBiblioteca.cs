using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace RockolaKaraoke.Servicios
{
    public class ResultadoEscaneo
    {
        public int ArchivosEncontrados { get; set; }
        public int Nuevos { get; set; }
        public int Actualizados { get; set; }
        public int Omitidos { get; set; }
        public int Errores { get; set; }
    }

    public static class EscanerBiblioteca
    {
        private static readonly HashSet<string> Extensiones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg",
            ".mp4", ".mkv", ".avi", ".wmv", ".mov", ".mpeg", ".mpg"
        };

        public static ResultadoEscaneo Escanear(IEnumerable<string> carpetas)
        {
            return Escanear(carpetas, null);
        }

        public static ResultadoEscaneo Escanear(IEnumerable<string> carpetas, long? generoId)
        {
            ResultadoEscaneo resultado = new ResultadoEscaneo();
            using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
            {
                conexion.Open();
                using (SQLiteTransaction transaccion = conexion.BeginTransaction())
                {
                    foreach (string carpeta in carpetas)
                    {
                        if (String.IsNullOrWhiteSpace(carpeta) || !Directory.Exists(carpeta))
                        {
                            resultado.Errores++;
                            continue;
                        }

                        string[] archivos;
                        try { archivos = Directory.GetFiles(carpeta, "*.*", SearchOption.AllDirectories); }
                        catch (UnauthorizedAccessException) { resultado.Errores++; continue; }
                        catch (IOException) { resultado.Errores++; continue; }

                        foreach (string archivo in archivos)
                        {
                            if (!Extensiones.Contains(Path.GetExtension(archivo))) continue;
                            resultado.ArchivosEncontrados++;
                            try { GuardarArchivo(conexion, transaccion, archivo, resultado, generoId); }
                            catch (SQLiteException) { resultado.Errores++; }
                            catch (IOException) { resultado.Errores++; }
                            catch (UnauthorizedAccessException) { resultado.Errores++; }
                        }
                    }
                    transaccion.Commit();
                }
            }
            return resultado;
        }

        private static void GuardarArchivo(SQLiteConnection conexion, SQLiteTransaction transaccion, string ruta, ResultadoEscaneo resultado, long? generoId)
        {
            string nombreArchivo = Path.GetFileNameWithoutExtension(ruta);
            string artista = Path.GetDirectoryName(ruta);
            artista = Path.GetFileName(artista.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string titulo = nombreArchivo;
            int separador = nombreArchivo.IndexOf(" - ", StringComparison.Ordinal);
            if (separador > 0 && separador < nombreArchivo.Length - 3)
            {
                artista = nombreArchivo.Substring(0, separador).Trim();
                titulo = nombreArchivo.Substring(separador + 3).Trim();
            }
            if (String.IsNullOrWhiteSpace(artista)) artista = "Artista desconocido";
            if (String.IsNullOrWhiteSpace(titulo)) titulo = nombreArchivo;

            string extension = Path.GetExtension(ruta).ToLowerInvariant();
            string tipo = (extension == ".mp4" || extension == ".mkv" || extension == ".avi" ||
                extension == ".wmv" || extension == ".mov" || extension == ".mpeg" || extension == ".mpg")
                ? "Video" : "Musica";

            long artistaId;
            using (SQLiteCommand cmd = new SQLiteCommand("INSERT OR IGNORE INTO Artistas(Nombre) VALUES(@nombre); SELECT Id FROM Artistas WHERE Nombre=@nombre COLLATE NOCASE;", conexion, transaccion))
            {
                cmd.Parameters.AddWithValue("@nombre", artista);
                artistaId = Convert.ToInt64(cmd.ExecuteScalar());
            }

            long existenteId = 0;
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT Id FROM Canciones WHERE RutaArchivo=@ruta;", conexion, transaccion))
            {
                cmd.Parameters.AddWithValue("@ruta", ruta);
                object valor = cmd.ExecuteScalar();
                if (valor != null && valor != DBNull.Value) existenteId = Convert.ToInt64(valor);
            }

            if (existenteId == 0)
            {
                using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO Canciones(GeneroId,ArtistaId,Titulo,RutaArchivo,Tipo) VALUES(@genero,@artista,@titulo,@ruta,@tipo);", conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@genero", (object)generoId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@artista", artistaId);
                    cmd.Parameters.AddWithValue("@titulo", titulo);
                    cmd.Parameters.AddWithValue("@ruta", ruta);
                    cmd.Parameters.AddWithValue("@tipo", tipo);
                    cmd.ExecuteNonQuery();
                }
                resultado.Nuevos++;
            }
            else
            {
                using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Canciones SET GeneroId=IFNULL(@genero,GeneroId),ArtistaId=@artista,Titulo=@titulo,Tipo=@tipo,Activa=1 WHERE Id=@id;", conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@genero", (object)generoId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@artista", artistaId);
                    cmd.Parameters.AddWithValue("@titulo", titulo);
                    cmd.Parameters.AddWithValue("@tipo", tipo);
                    cmd.Parameters.AddWithValue("@id", existenteId);
                    cmd.ExecuteNonQuery();
                }
                resultado.Actualizados++;
            }
        }
    }
}
