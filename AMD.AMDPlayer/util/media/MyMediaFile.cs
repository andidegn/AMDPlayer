using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AMD.util.media {
    public class MyMediaFile {
        public string Title { get; set; }
        public Uri FilePath { get; set; }

        public MyMediaFile(string title, Uri file_path) {
            // TODO: Complete member initialization
            this.Title = title;
            this.FilePath = file_path;
        }

        public override bool Equals(object obj) {
            MyMediaFile other = obj as MyMediaFile;
            return other == null ? false : other.Title.Equals(this.Title) && other.FilePath.Equals(this.FilePath);
        }

        public override string ToString() {
            return Title;
        }
    }
}
