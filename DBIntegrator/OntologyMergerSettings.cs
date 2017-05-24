using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF.Ontology;

namespace DBIntegrator
{
    class OntologyMergerSettings
    {
        public string Ontology1Path { get; set; }
        public string Ontology2Path { get; set; }
        public string Ontology1FileName
        {
            get
            {
                if (Ontology1Path.Length > 0)
                {
                    return Path.GetFileName(Ontology1Path);
                }
                else
                    return string.Empty;
            }
        }
        public string Ontology2FileName
        {
            get
            {
                if (Ontology2Path.Length > 0)
                {
                    return Path.GetFileName(Ontology2Path);
                }
                else
                    return string.Empty;
            }
        }
        public OntologyGraph Ontology1 { get; set; }
        public OntologyGraph Ontology2 { get; set; }
    }
}
