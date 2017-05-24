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
        List<Type> ontologyMergers = new List<Type>();

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

            //get all available ontology mergers
            interfaceType = typeof(IOntologyMerger);
            classes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(interfaceType.IsAssignableFrom).ToList();
            classes.Remove(typeof(IOntologyMerger));

            foreach (var t in classes)
            {
                this.ontologyMergers.Add(t);
                
                this.comboOntologyMergeEngine.Items.Add(t.Name);
            }
            this.comboOntologyMergeEngine.SelectedIndex = 0;
            //------------END->get all ontology mergers------------
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
                TreeViewItemOntologyInfo.SavedFileName = saveDlg.FileName;

                OntologyGraph ontologyGraph = g as OntologyGraph;
                UpdateTreeView(ontologyGraph);

            }
        }

        private void UpdateTreeView(OntologyGraph ontologyGraph)
        {
            this.ontologyTreeView.Items.Clear();
            TreeViewItem thingItem = new TreeViewItem() { Header = "Thing" };

            foreach (var oclass in ontologyGraph.AllClasses)
            {
                TreeViewItem classItem = GetTreeViewItem(oclass.ToString(), OntologyObjectType.CLASS);
                classItem.Tag = new TreeViewItemOntologyInfo
                {
                    OntologyGraph = ontologyGraph,
                    Type = OntologyObjectType.CLASS,
                    URI = oclass.ToString()
                };

                //add DataType properties
                List<Triple> classProps = ontologyGraph.GetTriplesWithPredicateObject(ontologyGraph.CreateUriNode("rdfs:domain"), ontologyGraph.CreateUriNode(new Uri(oclass.ToString()))).ToList();
                classProps.RemoveAll(t => ontologyGraph.OwlObjectProperties.Where(p => p.ToString() == t.Subject.ToString()).FirstOrDefault() != null);

                foreach (var dtTriple in classProps)
                {
                    TreeViewItem dtPropItem = GetTreeViewItem(dtTriple.Subject.ToString(), OntologyObjectType.DATATYPE_PROPERTY);
                    dtPropItem.Tag = new TreeViewItemOntologyInfo
                    {
                        OntologyGraph = ontologyGraph,
                        URI = dtTriple.Subject.ToString(),
                        Type = OntologyObjectType.DATATYPE_PROPERTY,
                        Domain = oclass.ToString()
                    };

                    //get range of this dataType property
                    var tripleRange = ontologyGraph.GetTriplesWithSubjectPredicate(ontologyGraph.CreateUriNode(new Uri(dtTriple.Subject.ToString())), ontologyGraph.CreateUriNode("rdfs:range")).FirstOrDefault();
                    if (tripleRange != null)
                    {
                        (dtPropItem.Tag as TreeViewItemOntologyInfo).Range = tripleRange.Object.ToString();
                    }
                    else (dtPropItem.Tag as TreeViewItemOntologyInfo).Range = "NO RANGE DEFINED";

                    classItem.Items.Add(dtPropItem);
                }

                //add Object properties
                var objectProps = ontologyGraph.OwlObjectProperties.Where(p => p.Domains.Contains(oclass));
                foreach (var objProp in objectProps)
                {
                    TreeViewItem objPropItem = GetTreeViewItem(objProp.ToString(), OntologyObjectType.OBJECT_PROPERTY);
                    objPropItem.Tag = new TreeViewItemOntologyInfo
                    {
                        URI = objProp.ToString(),
                        Domain = oclass.ToString(),
                        Range = string.Join("\n", objProp.Ranges),
                        OntologyGraph = ontologyGraph,
                        Type = OntologyObjectType.OBJECT_PROPERTY
                    };
                    classItem.Items.Add(objPropItem);
                }

                thingItem.Items.Add(classItem);

            }
            this.ontologyTreeView.Items.Add(thingItem);
        }

        enum OntologyObjectType
        {
            CLASS,
            DATATYPE_PROPERTY,
            OBJECT_PROPERTY
        }

        class TreeViewItemOntologyInfo
        {
            public string URI { get; set; }
            public string Domain { get; set; }
            public string Range { get; set; }
            public OntologyObjectType Type { get; set; }
            public OntologyGraph OntologyGraph { get; set; }
            public static string SavedFileName { get; set; }
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

        private void ontologyTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem selected = e.NewValue as TreeViewItem;
            TreeViewItemOntologyInfo ontologyInfo = selected.Tag as TreeViewItemOntologyInfo;

            if (ontologyInfo.Type == OntologyObjectType.DATATYPE_PROPERTY)
            {
                lblDomain.Content = ontologyInfo.Domain;
                lblRange.Content = ontologyInfo.Range;
                lblType.Content = "owl:DataTypeProperty";

                chkIsIFP.IsEnabled = false;
                
            }
            else if(ontologyInfo.Type == OntologyObjectType.OBJECT_PROPERTY)
            {
                chkIsIFP.IsEnabled = true;
                chkIsIFP.IsChecked = IsInverseFunctionalObjProp(ontologyInfo);

                lblDomain.Content = ontologyInfo.Domain;
                lblRange.Content = ontologyInfo.Range;
                lblType.Content = "owl:ObjectProperty";
            }
            else
            { 
                chkIsIFP.IsEnabled = false;
            }
        }

        private bool IsInverseFunctionalObjProp(TreeViewItemOntologyInfo ontologyInfo)
        {
            OntologyGraph ograph = ontologyInfo.OntologyGraph;

            INode pred = ograph.CreateUriNode("rdf:type");
            INode obj = ograph.CreateUriNode("owl:InverseFunctionalProperty");

            List<Triple> ifps = ograph.GetTriplesWithPredicateObject(pred, obj).ToList();

            return ifps.FirstOrDefault(t => t.Subject.ToString() == ontologyInfo.URI) != null;
        }

        private void chkIsIFP_Checked(object sender, RoutedEventArgs e)
        {
            //add IFP property for ontology ObjectProperty
            TreeViewItem selectedTVI = this.ontologyTreeView.SelectedItem as TreeViewItem;
            TreeViewItemOntologyInfo ontologyInfo = selectedTVI.Tag as TreeViewItemOntologyInfo;

            OntologyGraph ograph = ontologyInfo.OntologyGraph;
            OntologyProperty oprop = ograph.OwlObjectProperties.Where(p => p.ToString() == ontologyInfo.URI).First();

            INode pred = ograph.CreateUriNode("rdf:type");
            INode obj = ograph.CreateUriNode("owl:InverseFunctionalProperty");
            ograph.Assert(new Triple(oprop.Resource, pred, obj));

            btnSaveChanges.IsEnabled = true;
        }

        private void btnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem tvi = this.ontologyTreeView.SelectedItem as TreeViewItem;
            OntologyGraph ograph = (tvi.Tag as TreeViewItemOntologyInfo).OntologyGraph;
            ograph.SaveToFile(TreeViewItemOntologyInfo.SavedFileName);

            btnSaveChanges.IsEnabled = false;
        }

        private void btnAddMClasses_Click(object sender, RoutedEventArgs e)
        {
            groupMClasses.IsEnabled = false;
        }

        private void btnAddMProps_Click(object sender, RoutedEventArgs e)
        {
            groupMClasses.IsEnabled = true;
        }

        #region Ontology Merger Tab
        private string ShowOpenOntologyDialog() //returns ontology path
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = "Ontology Files|*.owl";

            if (openDlg.ShowDialog() == true)
            {
                return openDlg.FileName;
            }
            return null;
        }

        private string ShowSaveOntologyDialog()
        {
            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.Filter = "Ontology Files|*.owl";
            string ontology1Name = System.IO.Path.GetFileNameWithoutExtension(this.txtOntologyName1.Text);
            string ontology2Name = System.IO.Path.GetFileNameWithoutExtension(this.txtOntologyName2.Text);
            saveDlg.FileName = $"Merged_{ontology1Name}_{ontology2Name}.owl"; //proposed file name

            if (saveDlg.ShowDialog() == true)
            {
                return saveDlg.FileName;
            }
            else
                return null;
        }

        private void btnOpenOntology1_Click(object sender, RoutedEventArgs e)
        {
            string ontologyPath = ShowOpenOntologyDialog();
            if(ontologyPath!= null)
            {
                this.txtOntologyName1.Text = ontologyPath;
            }
        }
        

        private void btnOpenOntology2_Click(object sender, RoutedEventArgs e)
        {
            string ontologyPath = ShowOpenOntologyDialog();
            if (ontologyPath != null)
            {
                this.txtOntologyName2.Text = ontologyPath;
            }
        }
        

        private async void btnAutomaticMerge_Click(object sender, RoutedEventArgs e)
        {
            if(!File.Exists(this.txtOntologyName1.Text))
            {
                MessageBox.Show("Ontology #1 file doesn't exist! Check settings section.", "Error! File not found", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if(!File.Exists(this.txtOntologyName2.Text))
            {
                MessageBox.Show("Ontology #2 file doesn't exist! Check settings section.", "Error! File not found", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            //create instance of selected in settings merger engine:
            Type mergeEngine = this.ontologyMergers[this.comboOntologyMergeEngine.SelectedIndex];
            Type ioMergerInterface = mergeEngine.GetInterface("IOntologyMerger");
            if(ioMergerInterface == null)
            {
                MessageBox.Show("Selected merger engine does not implement IOntologyMerger -> automatic merging is not supported!",
                     "Error! Automatic merging is not supported", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IOntologyMerger merger = Activator.CreateInstance(mergeEngine) as IOntologyMerger;
            if(merger== null)
            {
                MessageBox.Show("Unable to create merger engine instance!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OntologyGraph gLMS = new OntologyGraph();
            gLMS.LoadFromFile(this.txtOntologyName1.Text);

            OntologyGraph gKMS = new OntologyGraph();
            gKMS.LoadFromFile(this.txtOntologyName2.Text);

            merger.Initialize(gLMS, gKMS);

            IProgress<double> progress = new Progress<double>(pValue=>UpdateProgressBar((int)(pValue*100)));


            this.oMergerOperationGrid.Visibility = Visibility.Hidden;
            this.mergerOperationButtons.IsEnabled = false;

            double thresholdClass = this.sliderThresholdClasses.Value;
            double thresholdProperty = this.sliderThresholdProperties.Value;
            IGraph mergedIGraph = await Task.Factory.StartNew<IGraph>(() => {
                                        return  merger.MergeTwoOntologies(o1: gKMS,
                                                                          o2: gLMS,
                                                                          classThreshold: thresholdClass,
                                                                          propertyThreshold: thresholdProperty,
                                                                          progress: progress);
            });

            OntologyGraph merged = mergedIGraph as OntologyGraph;

            string savePath = ShowSaveOntologyDialog();
            if(savePath!= null)
            {
                merged.SaveToFile(savePath);
            }

            this.oMergerOperationGrid.Visibility = Visibility.Visible; //restore visibility of operation grid
            this.mergerOperationButtons.IsEnabled = true;
        }

        private void UpdateProgressBar(int percent)
        {
            if(this.progressBarMerge.Value > 80 && percent<10)  //previous value is 100% and now it's 0 -> stage II
            {
                this.txtMergeStage.Content = "Stage 2/2: Generating federated schema";
            }
            this.progressBarMerge.Value = percent;
        }
        #endregion
    }
}
