using SPARQLtoSQL;
using System;
using System.Collections.Generic;
using System.Data;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using VDS.RDF.Query;

using System.IO;
using Microsoft.Win32;
using VDS.RDF.Ontology;
using MappingGenerator;
using VDS.RDF;

namespace DBIntegrator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<Type> dbLoaderTypes = new List<Type>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //get all available DB Loaders
            var interfaceType = typeof(IDBLoader);
            var classes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(interfaceType.IsAssignableFrom).ToList();
            classes.Remove(typeof(IDBLoader));

            foreach (var t in classes)
            {
                this.dbLoaderTypes.Add(t);
                this.comboDB1Loader.Items.Add(t.Name);
                this.comboDB2Loader.Items.Add(t.Name);
            }
            this.comboDB1Loader.SelectedIndex = 0;
            this.comboDB2Loader.SelectedIndex = 0;
            //------------END->get all available DB Loaders------------
        }

        private void btnExecuteQuery_Click(object sender, RoutedEventArgs e)
        {
            string query = new TextRange(this.queryRichTBox.Document.ContentStart, this.queryRichTBox.Document.ContentEnd).Text;
            string ontologyPath = this.txtOntoPath.Text;

            QuantumQueryProcessor qProcessor = new QuantumQueryProcessor(ontologyPath);

            qProcessor.AddDBInfo(this.txtConnString1.Text, this.txtDB1URI.Text, 
                dbLoaderTypes.Find(t=>t.Name==this.comboDB1Loader.SelectedValue.ToString()));
            qProcessor.AddDBInfo(this.txtConnString2.Text, this.txtDB2URI.Text,
                dbLoaderTypes.Find(t => t.Name == this.comboDB1Loader.SelectedValue.ToString()));

            //SparqlResultSet results =
            //    qProcessor.ExecuteSparql(@" SELECT *
            //                            WHERE { ?subj ?pred <http://www.example.org/FEDERATED/User/KMS.Id_User.95>.}");

            try {
                SparqlResultSet results = qProcessor.ExecuteSparql(query);
                DataTable resultDt = qProcessor.ConvertSparqlResultSetToDataTable(results);

                this.resultsQueryDataGrid.DataContext = resultDt.DefaultView;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Error executing query!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnBrowseOntology_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = "Ontology Files|*.owl";

            if(openDlg.ShowDialog() == true)
            {
                this.txtOntoPath.Text = openDlg.FileName;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void btnGenerateOntology_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.Filter = "Ontology Files|*.owl";
            saveDlg.FileName = this.txtDbName_MapGen.Text + ".owl"; //proposed file name

            if (saveDlg.ShowDialog() == true)
            {
                DBSemanticsGenerator generator = new DBSemanticsGenerator(this.txtConnString_MapGen.Text);
                IGraph g = generator.GenerateOntologyFromDB(this.txtDbName_MapGen.Text,
                                                 $"xmlns: {this.txtPrefix_MapGen.Text} =\"{this.txtDBURI_MapGen.Text}\"", saveDlg.FileName);

                OntologyGraph ontologyGraph = g as OntologyGraph;

                this.ontologyTreeView.Items.Clear();
                TreeViewItem thingItem = new TreeViewItem() { Header = "Thing" };

                foreach(var oclass in ontologyGraph.AllClasses)
                {
                    TreeViewItem classItem = GetTreeViewItem(oclass.ToString(), OntologyObjectType.CLASS);
                    

                    //add DataType properties
                    List<Triple> classProps = ontologyGraph.GetTriplesWithPredicateObject(ontologyGraph.CreateUriNode("rdfs:domain"), ontologyGraph.CreateUriNode(new Uri(oclass.ToString()))).ToList();
                    classProps.RemoveAll(t => ontologyGraph.OwlObjectProperties.Where(p => p.ToString() == t.Subject.ToString()).FirstOrDefault() != null);

                    foreach(var dtTriple in classProps)
                    {
                        classItem.Items.Add(GetTreeViewItem(dtTriple.Subject.ToString(), OntologyObjectType.DATATYPE_PROPERTY));
                    }

                    //add Object properties
                    var objectProps = ontologyGraph.OwlObjectProperties.Where(p => p.Domains.Contains(oclass));
                    foreach(var objProp in objectProps)
                    {
                        classItem.Items.Add(GetTreeViewItem(objProp.ToString(), OntologyObjectType.OBJECT_PROPERTY));
                    }

                    thingItem.Items.Add(classItem);

                }
                this.ontologyTreeView.Items.Add(thingItem);
                
            }
        }

        enum OntologyObjectType
        {
            CLASS,
            DATATYPE_PROPERTY,
            OBJECT_PROPERTY
        }

        private TreeViewItem GetTreeViewItem(string text, OntologyObjectType type)
        {
            TreeViewItem item = new TreeViewItem();

            item.IsExpanded = true;

            // create stack panel
            StackPanel stack = new StackPanel();
            stack.Orientation = Orientation.Horizontal;

            string imgName = string.Empty;

            switch(type)
            {
                case OntologyObjectType.CLASS: imgName = "ontology_class_25x25.png";
                    break;
                case OntologyObjectType.DATATYPE_PROPERTY:
                    imgName = "ontology_dataTypeProperty.png";
                    break;
                case OntologyObjectType.OBJECT_PROPERTY:
                    imgName = "ontology_Object.png";
                    break;
            }

            // create Image
            Image image = new Image();

            image.Source = new BitmapImage(new Uri(@"/Images/" + imgName, UriKind.Relative));
            //TextBlock tempTextBlock = new TextBlock();
            //tempTextBlock.Inlines.Add(image);
            //tempTextBlock.Inlines.Add(text);
            //item.Header = tempTextBlock;
            image.Width = 15;
            image.Height = 15;

            //return item;

            // Label
            Label lbl = new Label();
            lbl.Content = text;


            // Add into stack
            stack.Children.Add(image);
            stack.Children.Add(lbl);

            // assign stack to header
            item.Header = stack;
            return item;
        }
    }
}
