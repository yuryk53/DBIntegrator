using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VDS.RDF;
using VDS.RDF.Ontology;

namespace DBIntegrator
{
    /// <summary>
    /// Interaction logic for ObjectPropertySettings.xaml
    /// </summary>
    public partial class ObjectPropertySettingsWindow : Window
    {
        MainWindow.TreeViewItemOntologyInfo tviInfo = null;
        event Action<MainWindow.TreeViewItemOntologyInfo> OnPropertyConverted = null;

        internal ObjectPropertySettingsWindow(MainWindow.TreeViewItemOntologyInfo tviInfo, Action<MainWindow.TreeViewItemOntologyInfo> onPropertyConverted)
        {
            InitializeComponent();
            this.tviInfo = tviInfo;
            if (onPropertyConverted != null)
            {
                OnPropertyConverted += onPropertyConverted;
            }
            InitLayout();
        }

        private void InitLayout()
        {
            this.lblDomain.Content = tviInfo.Domain;
            this.txtPropURI.Text = tviInfo.URI;

            //add standard FOAF terms
            foreach(var foafTerm in Properties.Settings.Default.foafTerms)
            {
                this.comboRange.Items.Add(foafTerm.ToString());
            }

            //add classes from current ontology
            foreach (var ontoClass in tviInfo.OntologyGraph.AllClasses)
            {
                string qname = ontoClass.ToString();
                tviInfo.OntologyGraph.NamespaceMap.ReduceToQName(ontoClass.ToString(), out qname);
                this.comboRange.Items.Add(qname);   
            }
            this.comboRange.SelectedIndex = 0;
        }

        private void btnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            /*
                1. Find old datatype property
                2. Delete old datatype property
                3. Create new object property
                4. Add new object property
                5. Add new user-defined range
            */

            //1. Find old datatype property
            OntologyGraph ontology = tviInfo.OntologyGraph;
            INode subj = ontology.CreateUriNode(new Uri(tviInfo.URI));
            INode pred = ontology.CreateUriNode("rdf:type");
            INode obj = ontology.CreateUriNode("owl:DataTypeProperty");


            //2. Delete old datatype property & its ranges
            List<Triple> dtRangeTriples = ontology.GetTriplesWithSubjectPredicate(subj, ontology.CreateUriNode("rdfs:range")).ToList();
            bool b1 = ontology.Retract(dtRangeTriples);
            ontology.Retract(subj, pred, obj);
            

            //3. Create new object property
            obj = ontology.CreateUriNode("owl:ObjectProperty");

            //4. Add new object property
            ontology.Assert(subj, pred, obj);

            //5. Add new range
            //rdfs:range rdf:resource="&kms;User"
            string rangeUserDefined = this.comboRange.SelectedItem.ToString();
            if (rangeUserDefined.Contains("foaf:") && !ontology.NamespaceMap.HasNamespace("foaf"))
            {
                ontology.NamespaceMap.AddNamespace("foaf", new Uri(Properties.Settings.Default.foafUri));
            }
            pred = ontology.CreateUriNode("rdfs:range");
            obj = ontology.CreateUriNode(rangeUserDefined);
            ontology.Assert(subj, pred, obj);

            if (OnPropertyConverted != null)
            {
                tviInfo.Range = this.comboRange.SelectedItem.ToString();
                tviInfo.Domain = this.lblDomain.Content.ToString();
                tviInfo.Type = MainWindow.OntologyObjectType.OBJECT_PROPERTY;

                OnPropertyConverted(tviInfo); //after the conversion was done
            }
            this.Close();
        }
    }
}
