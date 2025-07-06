using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
            
            // Reset progress bar
            progressBar1.Value = 0;
            
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

        // ==== NEW TEST CODE - FILE SIZE CALCULATION ====
        private long CalculateTotalFileSize(string folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    return 0;

                string[] allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                long totalSize = 0;

                foreach (string file in allFiles)
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        totalSize += fileInfo.Length;
                    }
                    catch
                    {
                        // Skip files that can't be accessed
                        continue;
                    }
                }

                return totalSize;
            }
            catch
            {
                return 0;
            }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }
        // ==== END NEW TEST CODE ====

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
                // Use OpenFileDialog with modern Windows Explorer design for folder selection
                using (OpenFileDialog folderDialog = new OpenFileDialog())
                {
                    folderDialog.Title = "Select the main project folder to upload";
                    folderDialog.Filter = "All files (*.*)|*.*";
                    folderDialog.FileName = "Select this folder";
                    folderDialog.CheckFileExists = false;
                    folderDialog.CheckPathExists = true;
                    folderDialog.ValidateNames = false;
                    folderDialog.Multiselect = false;
                    
                    // Set initial directory to last selected path or user's documents
                    if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
                    {
                        folderDialog.InitialDirectory = selectedPath;
                    }
                    else
                    {
                        folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    }
                    
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Get the directory from the selected file path
                        selectedPath = Path.GetDirectoryName(folderDialog.FileName);
                        if (string.IsNullOrEmpty(selectedPath))
                        {
                            selectedPath = folderDialog.FileName; // Fallback
                        }
                        
                        // Ensure we have a valid directory path
                        if (File.Exists(selectedPath))
                        {
                            selectedPath = Path.GetDirectoryName(selectedPath);
                        }
                        
                        // Always show the full path in label3
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
                        
                        // ==== NEW TEST CODE - SHOW FILE SIZE INFO ====
                        long totalSize = CalculateTotalFileSize(selectedPath);
                        string[] files = Directory.GetFiles(selectedPath, "*.*", SearchOption.AllDirectories);
                        richTextBox1.AppendText($"Folder contains: {files.Length} files, Total size: {FormatFileSize(totalSize)}\n");
                        richTextBox1.ScrollToCaret();
                        // ==== END NEW TEST CODE ====
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
            /*
            // CLEAN SIMPLE UPLOAD CODE - WORKING VERSION (BACKUP)
            // Basic validation
            if (session == null || !session.Opened)
            {
                richTextBox1.AppendText("Error: Not connected to SFTP server.\n");
                richTextBox1.ScrollToCaret();
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                richTextBox1.AppendText("Error: No local path selected.\n");
                richTextBox1.ScrollToCaret();
                return;
            }

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                richTextBox1.AppendText("Error: Remote path is not set.\n");
                richTextBox1.ScrollToCaret();
                return;
            }

            // Store original path for restoration later
            string originalPath = label3.Text;

            try
            {
                richTextBox1.AppendText("Starting upload...\n");
                richTextBox1.ScrollToCaret();

                // Get all files from selected folder using array
                string[] allFiles = Directory.GetFiles(selectedPath, "*.*", SearchOption.AllDirectories);
                
                richTextBox1.AppendText($"Found {allFiles.Length} files to upload\n");
                richTextBox1.ScrollToCaret();

                // Reset progress bar to start clean
                progressBar1.Value = 0;

                // Upload files one by one using array
                for (int i = 0; i < allFiles.Length; i++)
                {
                    string currentFile = allFiles[i];
                    
                    // Show current file in label3
                    string fileName = Path.GetFileName(currentFile);
                    label3.Text = fileName;
                    
                    // Calculate relative path for remote upload
                    string relativePath = currentFile.Replace(selectedPath, "").TrimStart('\\', '/');
                    string remoteFilePath = remotePath + "/" + relativePath.Replace('\\', '/');
                    string remoteDir = Path.GetDirectoryName(remoteFilePath).Replace('\\', '/');
                    
                    // Set transfer options
                    TransferOptions transferOptions = new TransferOptions
                    {
                        TransferMode = TransferMode.Binary
                    };
                    
                    // Upload single file
                    session.PutFiles(currentFile, remoteDir + "/", false, transferOptions);
                    
                    // Update progress bar - simple calculation
                    progressBar1.Value = (i + 1) * 100 / allFiles.Length;
                    
                    // Update progress in rich text box
                    richTextBox1.AppendText($"✓ {i + 1}/{allFiles.Length}: {fileName}\n");
                    richTextBox1.ScrollToCaret();
                }
                
                richTextBox1.AppendText("Upload completed successfully!\n");
                richTextBox1.ScrollToCaret();
                
                // Keep progress bar at 100% to show completion
                progressBar1.Value = 100;
                
                // Wait 1 second then reset progress bar to clean state
                Task.Delay(1000).ContinueWith(t => 
                {
                    if (this.InvokeRequired)
                        this.Invoke(new Action(() => progressBar1.Value = 0));
                    else
                        progressBar1.Value = 0;
                });
                
                // Restore original full path in label3
                label3.Text = originalPath;
            }
            catch (Exception ex)
            {
                // Reset progress bar on error
                progressBar1.Value = 0;
                // Restore original path in case of error
                label3.Text = originalPath;
                richTextBox1.AppendText($"✗ Upload failed: {ex.Message}\n");
                richTextBox1.ScrollToCaret();
            }
            */
            
            // ==== NEW TEST CODE - FILE SIZE BASED UPLOAD PROGRESS ====
            // Basic validation
            if (session == null || !session.Opened)
            {
                richTextBox1.AppendText("Error: Not connected to SFTP server.\n");
                richTextBox1.ScrollToCaret();
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                richTextBox1.AppendText("Error: No local path selected.\n");
                richTextBox1.ScrollToCaret();
                return;
            }

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                richTextBox1.AppendText("Error: Remote path is not set.\n");
                richTextBox1.ScrollToCaret();
                return;
            }

            // Store original path for restoration later
            string originalPath = label3.Text;
            richTextBox1.Clear();

            try
            {
                richTextBox1.AppendText("Starting upload with file-size progress...\n");
                richTextBox1.ScrollToCaret();

                // Get all files and calculate total size
                string[] allFiles = Directory.GetFiles(selectedPath, "*.*", SearchOption.AllDirectories);
                long totalSize = CalculateTotalFileSize(selectedPath);
                long uploadedSize = 0;
                
                richTextBox1.AppendText($"Found {allFiles.Length} files, Total size: {FormatFileSize(totalSize)}\n");
                richTextBox1.ScrollToCaret();

                // Reset progress bar to start clean
                progressBar1.Value = 0;

                // Upload files one by one using array with size-based progress
                for (int i = 0; i < allFiles.Length; i++)
                {
                    string currentFile = allFiles[i];
                    
                    // Show current file in label3
                    string fileName = Path.GetFileName(currentFile);
                    label3.Text = fileName;
                    
                    // Get file size before upload
                    long fileSize = new FileInfo(currentFile).Length;
                    
                    // Calculate relative path for remote upload
                    string relativePath = currentFile.Replace(selectedPath, "").TrimStart('\\', '/');
                    string remoteFilePath = remotePath + "/" + relativePath.Replace('\\', '/');
                    string remoteDir = Path.GetDirectoryName(remoteFilePath).Replace('\\', '/');
                    
                    // Set transfer options
                    TransferOptions transferOptions = new TransferOptions
                    {
                        TransferMode = TransferMode.Binary
                    };
                    
                    // Upload single file
                    session.PutFiles(currentFile, remoteDir + "/", false, transferOptions);
                    
                    // Update uploaded size and progress bar based on file size
                    uploadedSize += fileSize;
                    int progressPercent = totalSize > 0 ? (int)((uploadedSize * 100) / totalSize) : 0;
                    progressBar1.Value = Math.Min(progressPercent, 100);
                    
                    // Update progress in rich text box with size info
                    richTextBox1.AppendText($"✓ {i + 1}/{allFiles.Length}: {fileName} ({FormatFileSize(fileSize)}) - {progressPercent}%\n");
                    richTextBox1.ScrollToCaret();
                }
                
                richTextBox1.AppendText($"Upload completed! Total uploaded: {FormatFileSize(totalSize)}\n");
                richTextBox1.ScrollToCaret();
                
                // Keep progress bar at 100% to show completion
                progressBar1.Value = 100;
                
                // Wait 1 second then reset progress bar to clean state
                Task.Delay(1000).ContinueWith(t => 
                {
                    if (this.InvokeRequired)
                        this.Invoke(new Action(() => progressBar1.Value = 0));
                    else
                        progressBar1.Value = 0;
                });
                
                // Restore original full path in label3
                label3.Text = originalPath;
            }
            catch (Exception ex)
            {
                // Reset progress bar on error
                progressBar1.Value = 0;
                // Restore original path in case of error
                label3.Text = originalPath;
                richTextBox1.AppendText($"✗ Upload failed: {ex.Message}\n");
                richTextBox1.ScrollToCaret();
            }
            // ==== END NEW TEST CODE ====
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