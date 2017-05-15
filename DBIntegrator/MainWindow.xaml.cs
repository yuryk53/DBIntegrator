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
    }
}
