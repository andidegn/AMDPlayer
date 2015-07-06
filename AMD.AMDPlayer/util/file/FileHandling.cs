using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMD.util.file {
    public class FileHandling {

        private static readonly string[] _video_extensions = {
            ".AVI", ".MP4", ".DIVX", ".WMV", ".MPEG", ".MGP", ".MKV", ".MOV"
        };
        private static readonly string[] _audio_extensions = {
            ".WAV", ".MID", ".MIDI", ".WMA", ".MP3", ".OGG", ".RMA", ".M4A", ".AAC"
        };
        private static readonly string[] _image_extensions = {
            ".PNG", ".JPG", ".JPEG", ".BMP", ".GIF"
        };


        public static bool? is_path_file(String full_path) {
            try {
                FileAttributes attr = File.GetAttributes(full_path);
                return (attr & FileAttributes.Directory) == FileAttributes.Directory ? false : true;
            } catch (Exception e) {
                if (AMD.AMDPlayer.App.DEBUG) {
                    Console.WriteLine(e.StackTrace);
                }
                return null;
            }
        }

        public static bool is_media_file(String path) {
            return is_audio_file(path) || is_image_file(path) || is_video_file(path);
        }

        public static bool is_video_file(String path) {
            return -1 != Array.IndexOf(_video_extensions, Path.GetExtension(path).ToUpperInvariant());
        }

        public static bool is_audio_file(String path) {
            return -1 != Array.IndexOf(_audio_extensions, Path.GetExtension(path).ToUpperInvariant());
        }

        public static bool is_image_file(String path) {
            return -1 != Array.IndexOf(_image_extensions, Path.GetExtension(path).ToUpperInvariant());
        }

        public static void get_files_in_directory(HashSet<String> set, params String[] folder_path) {
            if (folder_path == null) {
                return;
            }
            foreach (String path in folder_path) {
                if (is_path_file(path) == false) {
                    /* Works! */
                    var files = from file in Directory.GetFiles(path)
                                select file;
                    foreach (var file in files) {
                        set.Add(file);
                    }
                    var folders = from folder in Directory.GetDirectories(path)
                                  select folder;
                    get_files_in_directory(set, folders.ToArray<String>());


                    /* LINQ does not whork here, for some reason; throws the following exception:
                     * “System.ArgumentException: Second path fragment must not be a drive or UNC name”
                     * It does this when accessing a file on a network drive*/
                    //FileInfo fi = new FileInfo(path);
                    //DirectoryInfo di = fi.Directory;
                    //var files = from file in di.GetFiles(path)
                    //            select file;
                    //foreach (FileInfo file in files) {
                    //    set.Add(file.FullName);
                    //}
                    //var folders = from folder in di.GetDirectories(path)
                    //              select folder.FullName;
                    //get_files_in_directory(set, folders as String[]);



                    /* Only works for single files */
                    //DirectoryInfo di = (new FileInfo(path)).Directory;
                    //FileInfo[] files = di.GetFiles();
                    //foreach (FileInfo file in files) {
                    //    set.Add(file.FullName);
                    //}
                    //DirectoryInfo[] folders = di.GetDirectories();
                    //for (int i = 0; i < folders.Length; i++) {
                    //    get_files_in_directory(set, folders[i].FullName);
                    //}


                    /* Only works for single files */
                    //FileInfo fi = new FileInfo(path);
                    //DirectoryInfo di = fi.Directory;
                    //FileInfo[] files = di.GetFiles();
                    //foreach (FileInfo file in files) {
                    //    set.Add(file.FullName);
                    //}
                    //get_files_in_directory(set, di.GetDirectories().Select(f => f.FullName).ToArray<String>());
                } else {
                    set.Add(path);
                }
            }
            return;
        }

        public static String get_file_relative_to(String full_path, int offset) {
            String[] files = (from file in Directory.GetFiles(System.IO.Path.GetDirectoryName(full_path))
                        orderby file ascending
                        select file).ToArray<String>();
            files = Array.FindAll<String>(files, is_media_file);
            for (int i = 0; i < files.Count(); i++) {
                if (files.ElementAt(i).ToString().Equals(full_path)) {
                    if (i + offset > -1 && i + offset < files.Count()) {
                        return files.ElementAt(i + offset);
                    }
                }
            }
            return null;
        }

        public static OpenFileDialog MediaOpenFileDialog() {
            OpenFileDialog ofd = new OpenFileDialog();
            //FileFolderDialog ofd = new FileFolderDialog();
            ofd.Multiselect = true;
            //ofd.ValidateNames = false;
            //ofd.CheckFileExists = false;
            //ofd.CheckPathExists = true;
            StringBuilder sb_video = new StringBuilder();
            foreach (String extension in _video_extensions) {
                sb_video.Append('*');
                sb_video.Append(extension);
                sb_video.Append(";");
            }
            StringBuilder sb_audio = new StringBuilder();
            foreach (String extension in _audio_extensions) {
                sb_audio.Append('*');
                sb_audio.Append(extension);
                sb_audio.Append(";");
            }
            StringBuilder sb = new StringBuilder("Media files (*.avi;*.mp3;...|");
            sb.Append(sb_video.ToString());
            sb.Append(sb_audio.ToString());
            sb.Append("|Video files (*.avi;*.mkv;...|");
            sb.Append(sb_video.ToString());
            sb.Append("|Audio files (*.mp3;*.wav;...|");
            sb.Append(sb_audio.ToString());
            sb.Append("|Image files (*.jpg;*.png;...|");
            foreach (String extension in _image_extensions) {
                sb.Append('*');
                sb.Append(extension);
                sb.Append(";");
            }
            sb.Append("|All files (*.*)|*.*");
            ofd.Filter = sb.ToString();
            return ofd;
        }
    }
}
