﻿using SPARQLtoSQL;
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
using System.Collections.ObjectModel;

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

        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(this.ontoMergerTab.IsSelected)
            {
                PopulateOMergerComboBoxes();
                if(this.dataGridMPairs.ItemsSource == null)
                {
                    this.dataGridMPairs.ItemsSource = new ObservableCollection<SimilarClassPropertyDescription>();
                }
            }
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

        class SimilarClassPropDescrEqualityComparer : IEqualityComparer<SimilarClassPropertyDescription>
        {
            public bool Equals(SimilarClassPropertyDescription x, SimilarClassPropertyDescription y)
            {
                return x.ObjectName1 == y.ObjectName1 &&
                    x.SimilarityScore == y.SimilarityScore &&
                    x.MergeClassRelation == y.MergeClassRelation &&
                    x.MergePropRelation == y.MergePropRelation &&
                    x.SimObjectURI1 == y.SimObjectURI1 &&
                    x.SimObjectURI2 == y.SimObjectURI2;
            }

            public int GetHashCode(SimilarClassPropertyDescription obj)
            {
                return obj.GetHashCode();
            }
        }

        private void btnAddMClasses_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<SimilarClassPropertyDescription> pairs = this.dataGridMPairs.ItemsSource as ObservableCollection<SimilarClassPropertyDescription>;

            SimilarClassPropertyDescription pair = new SimilarClassPropertyDescription
            {
                ObjectName1 = this.comboMClass1.SelectedItem.ToString(),
                ObjectName2 = this.comboMClass2.SelectedItem.ToString(),
                MergeClassRelation = MergeClassRelation.SubClassOf,
                SimilarityScore = 1 //added by user
            };

            if (!pairs.Contains(pair, new SimilarClassPropDescrEqualityComparer()))
            {
                pairs.Add(pair);

                groupMClasses.IsEnabled = false;
            }
            else
            {
                MessageBox.Show("This merge pair has been already added!", "Error!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void btnAddMProps_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<SimilarClassPropertyDescription> pairs = this.dataGridMPairs.ItemsSource as ObservableCollection<SimilarClassPropertyDescription>;

            SimilarClassPropertyDescription pair = new SimilarClassPropertyDescription
            {
                ObjectName1 = this.comboMProp1.SelectedItem.ToString(),
                ObjectName2 = this.comboMProp2.SelectedItem.ToString(),
                MergePropRelation = MergePropertyRelation.EquivalentProperty,
                SimilarityScore = 1 //added by user
            };

            if (!pairs.Contains(pair, new SimilarClassPropDescrEqualityComparer()))
            {
                pairs.Add(pair);

                groupMClasses.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("This merge pair has been already added!", "Error!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
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
                PopulateOMergerComboBoxes(ontology1Changed: true);
            }
        }
        

        private void btnOpenOntology2_Click(object sender, RoutedEventArgs e)
        {
            string ontologyPath = ShowOpenOntologyDialog();
            if (ontologyPath != null)
            {
                this.txtOntologyName2.Text = ontologyPath;
                PopulateOMergerComboBoxes(ontology2Changed: true);
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

            this.txtMergeStage.Content = "Stage 1/2: Matching two ontologies";
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

        private void PopulateOMergerComboBoxes(bool ontology1Changed=false, bool ontology2Changed=false)
        {
            if (this.txtOntologyName1 != null && this.txtOntologyName2 != null)
            {
                //populate classes / properties combo boxes
                if (File.Exists(this.txtOntologyName1.Text) && File.Exists(this.txtOntologyName2.Text))
                {
                    if(ontology1Changed)
                    {
                        this.comboMClass1.Items.Clear();
                        this.comboMProp1.Items.Clear();
                        
                    }

                    if(ontology2Changed)
                    {
                        this.comboMClass2.Items.Clear();
                        this.comboMProp2.Items.Clear();
                    }

                    OntologyGraph ontology = null;
                    
                    if (this.comboMClass1.Items.Count == 0 && this.comboMProp1.Items.Count == 0)
                    {
                        ontology = new OntologyGraph();
                        ontology.LoadFromFile(this.txtOntologyName1.Text);

                        foreach (OntologyClass oclass in ontology.AllClasses)
                        {
                            this.comboMClass1.Items.Add(oclass.ToString());
                        }
                        this.comboMClass1.SelectedIndex = 0;


                        foreach (OntologyProperty objectProp in ontology.OwlObjectProperties)    //add object properties
                        {
                            this.comboMProp1.Items.Add(objectProp.ToString());
                        }

                        IEnumerable<Triple> dataTypePropTriples = ontology.GetTriplesWithPredicateObject(ontology.CreateUriNode("rdf:type"),
                            ontology.CreateUriNode("owl:DataTypeProperty"));

                        foreach (var triple in dataTypePropTriples)
                        {
                            this.comboMProp1.Items.Add(triple.Subject.ToString());
                        }
                        this.comboMProp1.SelectedIndex = 0;
                    }

                    if (this.comboMClass2.Items.Count == 0 && this.comboMProp2.Items.Count == 0)
                    {
                        ontology = new OntologyGraph();
                        ontology.LoadFromFile(this.txtOntologyName2.Text);

                        foreach (OntologyClass oclass in ontology.AllClasses)
                        {
                            this.comboMClass2.Items.Add(oclass.ToString());
                        }
                        this.comboMClass2.SelectedIndex = 0;

                        foreach (OntologyProperty objectProp in ontology.OwlObjectProperties)    //add object properties
                        {
                            this.comboMProp2.Items.Add(objectProp.ToString());
                        }

                        IEnumerable<Triple> dataTypePropTriples = ontology.GetTriplesWithPredicateObject(ontology.CreateUriNode("rdf:type"),
                            ontology.CreateUriNode("owl:DataTypeProperty"));

                        foreach (var triple in dataTypePropTriples)
                        {
                            this.comboMProp2.Items.Add(triple.Subject.ToString());
                        }
                        this.comboMProp2.SelectedIndex = 0;
                    }
                }
                else
                {
                    //expand expander for user
                    this.ontologyMergerExpander.IsExpanded = true;
                }
            }
        }

        private void groupMProps_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //filter combo boxes with properties, according to classes, selected in left-hand combo boxes
            if (this.comboMProp1 != null)
            {
                if (this.groupMProps.IsEnabled)
                {
                    this.comboMProp1.Items.Filter = (object o) =>
                    {
                        string ontologyPropertyURI = (string)o;
                        string selectedClassURI = this.comboMClass1.SelectedItem.ToString();
                        return ontologyPropertyURI.Contains(selectedClassURI);
                    };
                    this.comboMProp1.SelectedIndex = 0;
                }
            }

            //update combo box #2
            if (this.comboMProp2 != null)
            {
                if (this.groupMProps.IsEnabled)
                {
                    this.comboMProp2.Items.Filter = (object o) =>
                    {
                        string ontologyPropertyURI = (string)o;
                        string selectedClassURI = this.comboMClass2.SelectedItem.ToString();
                        return ontologyPropertyURI.Contains(selectedClassURI);
                    };
                    this.comboMProp2.SelectedIndex = 0;
                }
            }
        }

        

        private async void btnComputeMergePairs_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(this.txtOntologyName1.Text))
            {
                MessageBox.Show("Ontology #1 file doesn't exist! Check settings section.", "Error! File not found", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (!File.Exists(this.txtOntologyName2.Text))
            {
                MessageBox.Show("Ontology #2 file doesn't exist! Check settings section.", "Error! File not found", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            //create instance of selected in settings merger engine:
            Type mergeEngine = this.ontologyMergers[this.comboOntologyMergeEngine.SelectedIndex];
            Type ioMergerInterface = mergeEngine.GetInterface("IOntologyMerger");
            Type iiMergerInterface = mergeEngine.GetInterface("IInteractiveMerger");
            if (ioMergerInterface == null || iiMergerInterface ==null)
            {
                MessageBox.Show("Selected merger engine does not implement IOntologyMerger OR IInteractiveMerger interface!",
                     "Error! Semi-automatic merging is not supported", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IOntologyMerger merger = Activator.CreateInstance(mergeEngine) as IOntologyMerger;

            if (merger == null)
            {
                MessageBox.Show("Unable to create merger engine instance!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OntologyGraph ontology1 = new OntologyGraph();
            ontology1.LoadFromFile(this.txtOntologyName1.Text);

            OntologyGraph ontology2 = new OntologyGraph();
            ontology2.LoadFromFile(this.txtOntologyName2.Text);

            merger.Initialize(ontology1, ontology2);

            //get merge propositions for classes
            ObservableCollection<SimilarClassPropertyDescription> dataGridItems = this.dataGridMPairs.ItemsSource as ObservableCollection<SimilarClassPropertyDescription>;
            dataGridItems.Clear();
            Dictionary<string, List<SimilarClassPropertyDescription>> simDict =
                await Task.Factory.StartNew(()=>merger.GetSimilarOntologyClassesMatrix(progress: null));

            //pass mergeClassPairs through threshold
            List<SimilarClassPropertyDescription> similarClasses = new List<SimilarClassPropertyDescription>();
            foreach (var key in simDict.Keys)
            {
                SimilarClassPropertyDescription map = (from mapping
                                                       in simDict[key]
                                                       where mapping.SimilarityScore == simDict[key].Max(x => x.SimilarityScore)
                                                       select mapping).First(); //select pairs with highest similarity score
                similarClasses.Add(map);
                //dataGridItems.Add(map);     //add to datagrid
            }

            IInteractiveMerger iiMerger = merger as IInteractiveMerger;
            iiMerger.MergeOntologyClasses(
                similarClasses,
                simPair => {
                    bool canMerge = simPair.SimilarityScore >= this.sliderThresholdClasses.Value;
                    if (canMerge)
                    {
                        dataGridItems.Add(simPair); //add to datagrid
                    }
                    return canMerge;
                },        //no user interaction here
                propPair => {
                    bool canMerge = propPair.SimilarityScore >= this.sliderThresholdProperties.Value;
                    if (canMerge)
                    {
                        dataGridItems.Add(propPair);
                    }
                    return canMerge;
                },   //no user interaction here
                this.sliderThresholdProperties.Value,
                new ShortestFederatedNamesGenerator(),
                new XSDTypeCaster(),
                progress: null
                );
        }

        #endregion
    }
}
