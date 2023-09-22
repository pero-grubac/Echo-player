using NAudio.Wave;

namespace EchoPlayer
{
    public partial class Form1 : Form
    {

        private int echoLength;
        private string[] selectedFiles;
        private string selectedFolder;
        private static string extension = "_echo.wav";
        // private TaskScheduler taskScheduler;
        public Form1()
        {
            InitializeComponent();
            // taskScheduler = new TaskScheduler(Environment.ProcessorCount);
        }


        private void browseButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Audio Files|*.wav";
            openFileDialog.Title = "Select an Audio File";
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {

                selectedFiles = openFileDialog.FileNames;
                filePathTxtBox.Text = string.Join(", ", selectedFiles);

            }
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {

        }

        private void delayTxtBox_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(delayTxtBox.Text))
            {
                if (int.TryParse(delayTxtBox.Text, out echoLength))
                {


                }
            }
        }

        private async void downloadButton_Click(object sender, EventArgs e)
        {
            if (selectedFiles == null || selectedFiles.Length == 0)
            {
                MessageBox.Show("No audio files selected.");
                return;
            }

            if (string.IsNullOrEmpty(delayTxtBox.Text) || !int.TryParse(delayTxtBox.Text, out echoLength))
            {
                MessageBox.Show("Enter a valid delay.");
                return;
            }
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                selectedFolder = folderBrowserDialog.SelectedPath;
            }
            Task[] tasks = selectedFiles.Select(filePath => Task.Run(() =>
             {
                 AddEcho(filePath);
             })).ToArray();
            await Task.WhenAll(tasks);
            MessageBox.Show("Echo applied and audio files saved successfully.");

        }

        private void SaveAudio(string saveFilePath, BlockAlignReductionStream stream)
        {

            using (WaveFileWriter writer = new WaveFileWriter(saveFilePath, stream.WaveFormat))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                int totalBytesWritten = 0;
                while (totalBytesWritten < stream.Length)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                        break;
                    writer.Write(buffer, 0, bytesRead);
                    totalBytesWritten += bytesRead;
                }
            }
        }
        private void AddEcho(string audioFilePath)
        {
            if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
            {
                MessageBox.Show("Select valid audio file");
                return;
            }

            try
            {
                using (WaveChannel32 wave = new WaveChannel32(new WaveFileReader(audioFilePath)))
                {
                    EffectStream effect = new EffectStream(wave);
                    BlockAlignReductionStream stream = new BlockAlignReductionStream(effect);
                    int sampleRate = wave.WaveFormat.SampleRate;

                    for (int i = 0; i < wave.WaveFormat.Channels; i++)
                        effect.Echo.Add(new Echo(echoLength * sampleRate, 0.5f));

                    DirectSoundOut output = new DirectSoundOut(200);
                    output.Init(stream);


                    string fileName = audioFilePath.Substring(audioFilePath.LastIndexOf('\\') + 1);
                    string basicName = fileName.Substring(0, fileName.LastIndexOf('.'));
                    string saveFilePath = Path.Combine(selectedFolder, basicName + extension);
                    SaveAudio(saveFilePath, stream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}