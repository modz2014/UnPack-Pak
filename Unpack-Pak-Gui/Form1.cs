
using System.Diagnostics;

namespace Unpack_Pak_Gui
{
    public partial class Form1 : Form
    {

        private PakParser _pakParser;
        private string _sourceFilePath;
        private string _destinationFolderPath;
        public Form1()
        {
            InitializeComponent();
        }
        private void BtnBrowseSource_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "PAK Files (*.pak)|*.pak|All Files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    lblSourcePath.Text = openFileDialog.FileName;
                    _sourceFilePath = openFileDialog.FileName;
                    Debug.WriteLine($"Selected file path: {_sourceFilePath}");

                    try
                    {
                        _pakParser = new PakParser(File.OpenRead(_sourceFilePath));
                        PopulateListBox();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error opening PAK file: {ex.Message}");
                    }
                }
            }
        }

        private void BtnBrowseDestination_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    lblDestinationPath.Text = folderDialog.SelectedPath;
                    _destinationFolderPath = folderDialog.SelectedPath;
                }
            }
        }

        public void PopulateListBox()
        {
            lstFiles.Items.Clear(); 

            foreach (var file in _pakParser.List())
            {
                lstFiles.Items.Add(file); 
            }
        }

        private void BtnExtract_Click(object sender, EventArgs e)
        {
            if (_pakParser == null)
            {
                MessageBox.Show("Please load a PAK file first.");
                return;
            }

            if (lstFiles.SelectedItem == null)
            {
                MessageBox.Show("Please select a file to extract.");
                return;
            }

            string selectedFile = lstFiles.SelectedItem.ToString();

            selectedFile = selectedFile.Replace('\0', '_');

            try
            {
                string destinationPath = Path.Combine(_destinationFolderPath, selectedFile.Replace('/', Path.DirectorySeparatorChar));
                string directoryPath = Path.GetDirectoryName(destinationPath);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                try
                {
                    PakParser.Record record = _pakParser.Unpack(selectedFile);
                    MessageBox.Show($"File extraction was successful! Data length: {record.Data.Count} bytes.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error extracting file: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during extraction setup: {ex.Message}");
            }
        }

    }
}