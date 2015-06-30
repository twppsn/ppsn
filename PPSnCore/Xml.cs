using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn
{
    public class Xml
    {
        // Element- und Atribute-Namen des Daten-XML
        public static readonly string tag_data = "data";
        public static readonly string tag_table = "t";
        public static readonly string tag_row = "r";
        
        public static readonly string tag_value = "v";
        public static readonly string tag_originalValue = "o";
        public static readonly string tag_currentValue = "c";

        public static readonly string tag_rowState = "s";
        public static readonly string tag_rowAdded = "a";
        public static readonly string tag_dontKnow = "n"; //~rename

        public static readonly string tag_combine = "combine";
    }
}
