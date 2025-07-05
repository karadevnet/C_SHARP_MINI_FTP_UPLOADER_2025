using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using WinSCP;

namespace C_SHARP_MNI_FTP_UPLOADER_2025
{

    public partial class Form1 : Form
    {
        Session session = new WinSCP.Session();

        string selectedPath = string.Empty;
        string host = string.Empty;
        int port = 0;
        string username = string.Empty;
        string password = string.Empty;
        string fingerprint = string.Empty;
        string remotePath = string.Empty; // Adjust this to your remote path

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Clear all fields on program start
            textBox1.Text = "";      // Host
            textBox2.Text = "22";    // Port (keep default)
            textBox3.Text = "";      // Username
            textBox4.Text = "";      // Password
            textBox5.Text = "";      // Remote path
            textBox6.Text = "";      // Remote folder name
            
            // Clear path-related fields
            selectedPath = string.Empty;
            label3.Text = "";
            
            // Clear the output text box
            richTextBox1.Text = "";
            
            // Set button state
            button1.Text = "DISCONNECTED";
            button1.BackColor = System.Drawing.SystemColors.ButtonFace;
            
            // Add form closing event handler for cleanup
            this.FormClosing += Form1_FormClosing;
            
            // Welcome message
            richTextBox1.AppendText("SFTP Uploader ready. All fields cleared.\n");
            richTextBox1.ScrollToCaret();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {   // Handle form closing event to clean up resources
            // Clean up WinSCP session when form is closing
            if (session != null)
            {
                if (session.Opened)
                {
                    session.Close();
                }
                session.Dispose();
                session = null;
            }
        }

        private string GetSshHostKeyFingerprint(string host, int port)
        {       // Scan the SSH host key fingerprint using WinSCP
            using (Session scanSession = new Session())
            {
                SessionOptions scanOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = host,
                    PortNumber = port
                };
                return scanSession.ScanFingerprint(scanOptions, "SHA-256");
            }
        }

        private string EncodePassword(string password)
        {
            // Simple Base64 encoding for basic password protection
            if (string.IsNullOrEmpty(password))
                return "";
            
            byte[] bytes = Encoding.UTF8.GetBytes(password);
            return Convert.ToBase64String(bytes);
        }

        private string DecodePassword(string encodedPassword)
        {
            // Decode Base64 password
            if (string.IsNullOrEmpty(encodedPassword))
                return "";
            
            try
            {
                byte[] bytes = Convert.FromBase64String(encodedPassword);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return ""; // Return empty if decoding fails
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {   // BUTTON CONNECT/DISCONNECT
            if (session == null || !session.Opened)
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(textBox1.Text))
                {
                    richTextBox1.AppendText("Error: Host cannot be empty.\n");
                    richTextBox1.ScrollToCaret();
                    return;
                }

                if (string.IsNullOrWhiteSpace(textBox3.Text))
                {
                    richTextBox1.AppendText("Error: Username cannot be empty.\n");
                    richTextBox1.ScrollToCaret();
                    return;
                }

                if (string.IsNullOrWhiteSpace(textBox4.Text))
                {
                    richTextBox1.AppendText("Error: Password cannot be empty.\n");
                    richTextBox1.ScrollToCaret();
                    return;
                }

                if (!int.TryParse(textBox2.Text, out int portNumber) || portNumber < 1 || portNumber > 65535)
                {
                    richTextBox1.AppendText("Error: Port must be a valid number between 1 and 65535.\n");
                    richTextBox1.ScrollToCaret();
                    return;
                }

                if (string.IsNullOrWhiteSpace(textBox5.Text))
                {
                    richTextBox1.AppendText("Error: Remote path cannot be empty.\n");
                    richTextBox1.ScrollToCaret();
                    return;
                }

                // Assign validated values
                host = textBox1.Text.Trim();
                port = portNumber;
                username = textBox3.Text.Trim();
                password = textBox4.Text;
                remotePath = textBox5.Text.Trim();

                try
                {
                    fingerprint = GetSshHostKeyFingerprint(host, port);
                    //richTextBox1.AppendText($"Detected SSH fingerprint: {fingerprint}\n");
                    richTextBox1.AppendText($"Detected SSH fingerprint: OK\n");
                    richTextBox1.ScrollToCaret();
                }
                catch (Exception ex)
                {
                    richTextBox1.AppendText($"Error scanning fingerprint: {ex.Message}\n");
                    richTextBox1.ScrollToCaret();
                    return;
                }

                // Dispose previous session if any
                if (session != null)
                {
                    session.Dispose();
                }
                session = new WinSCP.Session();

                WinSCP.SessionOptions sessionOptions = new WinSCP.SessionOptions
                {
                    Protocol = WinSCP.Protocol.Sftp,
                    HostName = host,
                    UserName = username,
                    Password = password,
                    PortNumber = port,
                    SshHostKeyFingerprint = fingerprint,
                    Timeout = TimeSpan.FromSeconds(30) // Add 30 second timeout
                };

                try
                {
                    session.Open(sessionOptions);
                    richTextBox1.AppendText("Connected to SFTP server.\n");
                    richTextBox1.ScrollToCaret();
                    button1.Text = "CONNECTED";
                    button1.BackColor = Color.Lime;
                }
                catch (Exception ex)
                {
                    richTextBox1.AppendText($"Connection error: {ex.Message}\n");
                    richTextBox1.ScrollToCaret();
                    session.Dispose();
                    session = null;
                    button1.Text = "DISCONNECTED";
                    button1.BackColor = System.Drawing.SystemColors.ButtonFace;
                }
            }

            else if (session != null && session.Opened)
            {
                // If the session is already open, close it
                session.Close();
                richTextBox1.AppendText("Session closed.\n");
                richTextBox1.ScrollToCaret();
                button1.Text = "DISCONNECTED";
                button1.BackColor = System.Drawing.SystemColors.ButtonFace;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {       // BUTTON SELECT PATH LOCAL
            try
            {
                // Use FolderBrowserDialog for proper folder selection with "OK" button
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select the main project folder to upload";
                    folderDialog.ShowNewFolderButton = true;
                    
                    // Set initial directory to last selected path or user's documents
                    if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
                    {
                        folderDialog.SelectedPath = selectedPath;
                    }
                    else
                    {
                        folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    }
                    
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        selectedPath = folderDialog.SelectedPath;
                        label3.Text = selectedPath;
                        
                        // Update textBox6 with just the selected folder name
                        string folderName = Path.GetFileName(selectedPath);
                        if (string.IsNullOrEmpty(folderName))
                        {
                            // Handle root drives like C:\ 
                            folderName = selectedPath.Replace("\\", "").Replace(":", "");
                        }
                        textBox6.Text = "/" + folderName;
                        
                        richTextBox1.AppendText($"Selected local path: {selectedPath}\n");
                        richTextBox1.ScrollToCaret();
                    }
                    else
                    {
                        richTextBox1.AppendText("Local path selection cancelled.\n");
                        richTextBox1.ScrollToCaret();
                    }
                }
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText($"Error selecting local path: {ex.Message}\n");
                richTextBox1.ScrollToCaret();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {       // BUTTON UPLOAD
            // Check if session is connected
            if (session == null || !session.Opened)
            {
                richTextBox1.AppendText("Error: Not connected to SFTP server.\n");
                richTextBox1.ScrollToCaret();
                return;
            }

            // Check if a path is selected
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                richTextBox1.AppendText("Error: No local path selected.\n");
                richTextBox1.ScrollToCaret();
                return;
            }

            // Check if remote path is set
            if (string.IsNullOrWhiteSpace(remotePath))
            {
                richTextBox1.AppendText("Error: Remote path is not set.\n");
                richTextBox1.ScrollToCaret();
                return;
            }

            try
            {
                // Upload the selected path to the SFTP server
                TransferOptions transferOptions = new TransferOptions
                {
                    TransferMode = TransferMode.Binary
                };
                
                richTextBox1.AppendText("Starting upload...\n");
                richTextBox1.ScrollToCaret();
                TransferOperationResult transferResult = session.PutFiles(selectedPath, remotePath, false, transferOptions);
                
                // Check for errors and provide detailed feedback
                if (transferResult.IsSuccess)
                {
                    richTextBox1.AppendText($"Upload completed successfully! {transferResult.Transfers.Count} files uploaded.\n");
                    richTextBox1.ScrollToCaret();
                    foreach (TransferEventArgs transfer in transferResult.Transfers)
                    {
                        string relativePath = transfer.FileName.Replace(selectedPath, "").TrimStart('\\', '/');
                        richTextBox1.AppendText($"✓ Uploaded: {relativePath}\n");
                        richTextBox1.ScrollToCaret();
                    }
                }
                else
                {
                    richTextBox1.AppendText("Upload completed with errors:\n");
                    richTextBox1.ScrollToCaret();
                    foreach (TransferEventArgs transfer in transferResult.Transfers)
                    {
                        string relativePath = transfer.FileName.Replace(selectedPath, "").TrimStart('\\', '/');
                        if (transfer.Error == null)
                        {
                            richTextBox1.AppendText($"✓ Uploaded: {relativePath}\n");
                            richTextBox1.ScrollToCaret();
                        }
                        else
                        {
                            richTextBox1.AppendText($"✗ Failed: {relativePath} - {transfer.Error.Message}\n");
                            richTextBox1.ScrollToCaret();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText($"Upload error: {ex.Message}\n");
                richTextBox1.ScrollToCaret();
            }

            // 
        }

        private void button3_Click(object sender, EventArgs e)
        {           // BUTTON SAVE SETTINGS
            try
            {
                // Use SaveFileDialog to let user choose filename and location
                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.InitialDirectory = Application.StartupPath;
                    saveDialog.Filter = "Settings files (*.txt)|*.txt|All files (*.*)|*.*";
                    saveDialog.FileName = "MyProject_Settings.txt";
                    saveDialog.Title = "Save Settings File";
                    
                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Create settings content with encoded password
                        string settingsContent = $"host={textBox1.Text}\n";
                        settingsContent += $"port={textBox2.Text}\n";
                        settingsContent += $"username={textBox3.Text}\n";
                        settingsContent += $"password={EncodePassword(textBox4.Text)}\n";
                        settingsContent += $"remotepath={textBox5.Text}\n";
                        settingsContent += $"localpath={selectedPath}\n";
                        settingsContent += $"saved_date={DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                        
                        // Write to file
                        File.WriteAllText(saveDialog.FileName, settingsContent);
                        
                        string fileName = Path.GetFileName(saveDialog.FileName);
                        richTextBox1.AppendText($"Settings saved to: {fileName} (password encoded)\n");
                        richTextBox1.ScrollToCaret();
                    }
                    else
                    {
                        richTextBox1.AppendText("Save cancelled.\n");
                        richTextBox1.ScrollToCaret();
                    }
                }
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText($"Error saving settings: {ex.Message}\n");
                richTextBox1.ScrollToCaret();
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {            // BUTTON LOAD SETTINGS
            try
            {
                // Use OpenFileDialog to let user choose settings file to load
                using (OpenFileDialog openDialog = new OpenFileDialog())
                {
                    openDialog.InitialDirectory = Application.StartupPath;
                    openDialog.Filter = "Settings files (*.txt)|*.txt|All files (*.*)|*.*";
                    openDialog.Title = "Load Settings File";
                    openDialog.CheckFileExists = true;
                    openDialog.CheckPathExists = true;
                    openDialog.Multiselect = false;
                    
                    if (openDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Read and parse the settings file
                        string[] lines = File.ReadAllLines(openDialog.FileName);
                        int loadedCount = 0;
                        
                        foreach (string line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                                continue;
                                
                            string[] parts = line.Split(new char[] { '=' }, 2);
                            if (parts.Length != 2)
                                continue;
                                
                            string key = parts[0].Trim().ToLower();
                            string value = parts[1].Trim();
                            
                            switch (key)
                            {
                                case "host":
                                    textBox1.Text = value;
                                    loadedCount++;
                                    break;
                                case "port":
                                    textBox2.Text = value;
                                    loadedCount++;
                                    break;
                                case "username":
                                    textBox3.Text = value;
                                    loadedCount++;
                                    break;
                                case "password":
                                    // Decode the password
                                    textBox4.Text = DecodePassword(value);
                                    loadedCount++;
                                    break;
                                case "remotepath":
                                    textBox5.Text = value;
                                    loadedCount++;
                                    break;
                                case "localpath":
                                    if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                                    {
                                        selectedPath = value;
                                        label3.Text = selectedPath;
                                        
                                        // Update textBox6 with just the selected folder name
                                        string folderName = Path.GetFileName(selectedPath);
                                        if (string.IsNullOrEmpty(folderName))
                                        {
                                            // Handle root drives like C:\ 
                                            folderName = selectedPath.Replace("\\", "").Replace(":", "");
                                        }
                                        textBox6.Text = "/" + folderName;
                                        
                                        loadedCount++;
                                    }
                                    else if (!string.IsNullOrEmpty(value))
                                    {
                                        richTextBox1.AppendText($"Warning: Local path '{value}' does not exist.\n");
                                        richTextBox1.ScrollToCaret();
                                    }
                                    break;
                            }
                        }
                        
                        string fileName = Path.GetFileName(openDialog.FileName);
                        if (loadedCount > 0)
                        {
                            richTextBox1.AppendText($"Settings loaded from: {fileName} ({loadedCount} fields loaded)\n");
                            richTextBox1.ScrollToCaret();
                        }
                        else
                        {
                            richTextBox1.AppendText($"No valid settings found in: {fileName}\n");
                            richTextBox1.ScrollToCaret();
                        }
                    }
                    else
                    {
                        richTextBox1.AppendText("Load cancelled.\n");
                        richTextBox1.ScrollToCaret();
                    }
                }
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText($"Error loading settings: {ex.Message}\n");
                richTextBox1.ScrollToCaret();
            }
        }
    }
}