/* 
Copyright © 2017 Yurii Bilyk. All rights reserved. Contacts: <yuryk531@gmail.com>

This file is part of "Database integrator".

"Database integrator" is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

"Database integrator" is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with "Database integrator".  If not, see <http:www.gnu.org/licenses/>. 
*/
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
