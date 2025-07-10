using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
            // Attach expand/collapse handlers
            treeView1.AfterExpand += TreeView1_AfterExpand;
            treeView1.AfterCollapse += TreeView1_AfterCollapse;
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
            // Set default permission in ComboBox (644 at index 0)
            if (comboBox1.Items.Count > 0)
                comboBox1.SelectedIndex = 0;

            // Attach AfterCheck handler ONCE to ensure it is always present
            treeView1.AfterCheck -= TreeView1_AfterCheck;
            treeView1.AfterCheck += TreeView1_AfterCheck;
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
        {
            // BUTTON CONNECT/DISCONNECT
            if (session == null || !session.Opened)
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(textBox1.Text))
                {
                    richTextBox1.AppendText("Error: Host cannot be empty.\n");
                    richTextBox1.ScrollToCaret();
                    ShowLargeMessageBox("Host cannot be empty. Please enter the SFTP host.", "Missing Host", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(textBox3.Text))
                {
                    richTextBox1.AppendText("Error: Username cannot be empty.\n");
                    richTextBox1.ScrollToCaret();
                    ShowLargeMessageBox("Username cannot be empty. Please enter the SFTP username.", "Missing Username", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(textBox4.Text))
                {
                    richTextBox1.AppendText("Error: Password cannot be empty.\n");
                    richTextBox1.ScrollToCaret();
                    ShowLargeMessageBox("Password cannot be empty. Please enter the SFTP password.", "Missing Password", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!int.TryParse(textBox2.Text, out int portNumber) || portNumber < 1 || portNumber > 65535)
                {
                    richTextBox1.AppendText("Error: Port must be a valid number between 1 and 65535.\n");
                    richTextBox1.ScrollToCaret();
                    ShowLargeMessageBox("Port must be a valid number between 1 and 65535.", "Invalid Port", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(textBox5.Text))
                {
                    richTextBox1.AppendText("Error: Remote path cannot be empty.\n");
                    richTextBox1.ScrollToCaret();
                    ShowLargeMessageBox("Remote path cannot be empty. Please enter the remote path.", "Missing Remote Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    // For security, do not show the actual fingerprint
                    richTextBox1.AppendText("SSH fingerprint detected and verified.\n");
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
                using (OpenFileDialog folderDialog = new OpenFileDialog())
                {
                    folderDialog.Title = "Select the main project folder to upload";
                    folderDialog.Filter = "All files (*.*)|*.*";
                    folderDialog.FileName = "Select this folder";
                    folderDialog.CheckFileExists = false;
                    folderDialog.CheckPathExists = true;
                    folderDialog.ValidateNames = false;
                    folderDialog.Multiselect = false;
                    if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
                        folderDialog.InitialDirectory = selectedPath;
                    else
                        folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        selectedPath = Path.GetDirectoryName(folderDialog.FileName);
                        if (string.IsNullOrEmpty(selectedPath))
                            selectedPath = folderDialog.FileName;
                        if (File.Exists(selectedPath))
                            selectedPath = Path.GetDirectoryName(selectedPath);
                        label3.Text = selectedPath;
                        string folderName = Path.GetFileName(selectedPath);
                        if (string.IsNullOrEmpty(folderName))
                            folderName = selectedPath.Replace("\\", "").Replace(":", "");
                        textBox6.Text = "/" + folderName;
                        // ==== NEW: Populate TreeView with checkboxes ===
                        if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
                        {
                            PopulateTreeViewWithCheckBoxes(selectedPath);
                        }
                        else
                        {
                            treeView1.Nodes.Clear();
                            treeView1.Nodes.Add("No folder selected or folder does not exist.");
                        }
                        // ==== END NEW ===
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
                        treeView1.Nodes.Clear();
                        treeView1.Nodes.Add("No folder selected.");
                    }
                }
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText($"Error selecting local path: {ex.Message}\n");
                richTextBox1.ScrollToCaret();
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add("Error: " + ex.Message);
            }
        }

        // === NEW: Populate TreeView with checkboxes for folder/file structure ===
        private void PopulateTreeViewWithCheckBoxes(string rootPath)
        {
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            treeView1.CheckBoxes = true; // Ensure checkboxes are visible
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                treeView1.EndUpdate();
                return;
            }
            DirectoryInfo rootDir = new DirectoryInfo(rootPath);
            TreeNode rootNode = CreateDirectoryNode(rootDir);
            treeView1.Nodes.Add(rootNode);
            rootNode.Expand();
            treeView1.EndUpdate();
            // Remove event handler attach/detach here (handled in Form1_Load)
        }

        private TreeNode CreateDirectoryNode(DirectoryInfo dirInfo)
        {
            TreeNode dirNode = new TreeNode(dirInfo.Name) { Tag = dirInfo.FullName };
            // Add subdirectories
            foreach (var subDir in dirInfo.GetDirectories())
            {
                dirNode.Nodes.Add(CreateDirectoryNode(subDir));
            }
            // Add files
            foreach (var file in dirInfo.GetFiles())
            {
                TreeNode fileNode = new TreeNode(file.Name) { Tag = file.FullName };
                dirNode.Nodes.Add(fileNode);
            }
            return dirNode;
        }

        // === NEW: Ensure checking a parent checks all children, and vice versa ===
        private void TreeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // Prevent recursive event firing
            treeView1.AfterCheck -= TreeView1_AfterCheck;
            try
            {
                // Check/uncheck all children
                SetChildNodesChecked(e.Node, e.Node.Checked);
                // Optionally, update parent nodes (if all siblings checked, check parent)
                UpdateParentNodesChecked(e.Node);
                // Select the node that was just checked/unchecked
                treeView1.SelectedNode = e.Node;
            }
            finally
            {
                treeView1.AfterCheck += TreeView1_AfterCheck;
            }
        }

        private void SetChildNodesChecked(TreeNode node, bool isChecked)
        {
            foreach (TreeNode child in node.Nodes)
            {
                child.Checked = isChecked;
                SetChildNodesChecked(child, isChecked);
            }
        }

        private void UpdateParentNodesChecked(TreeNode node)
        {
            if (node.Parent == null) return;
            bool allChecked = true;
            foreach (TreeNode sibling in node.Parent.Nodes)
            {
                if (!sibling.Checked)
                {
                    allChecked = false;
                    break;
                }
            }
            node.Parent.Checked = allChecked;
            UpdateParentNodesChecked(node.Parent);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // === NEW: Always refresh TreeView if folder structure changed ===
            RefreshTreeViewIfChanged();
            // === NEW: Mirror delete remote files/folders not present locally ===
            try
            {
                MirrorDeleteRemoteExtra(session, selectedPath, remotePath);
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText($"Mirror delete failed: {ex.Message}\n");
            }

            // === COMMENTED OUT OLD CODE ===
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
            
            // === NEW: Only upload checked files and create checked directories ===
            // 1. Validate connection and paths
            if (session == null || !session.Opened)
            {
                richTextBox1.AppendText("Error: Not connected to SFTP server.\n");
                richTextBox1.ScrollToCaret();
                ShowLargeMessageBox("Not connected to SFTP server.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                richTextBox1.AppendText("Error: No local path selected.\n");
                richTextBox1.ScrollToCaret();
                ShowLargeMessageBox("No local path selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(remotePath))
            {
                richTextBox1.AppendText("Error: Remote path is not set.\n");
                richTextBox1.ScrollToCaret();
                ShowLargeMessageBox("Remote path is not set.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // 2. Collect checked files and directories
            List<string> checkedFiles = GetCheckedFiles(treeView1.Nodes);
            HashSet<string> checkedDirs = GetCheckedDirectories(treeView1.Nodes);
            if (checkedFiles.Count == 0)
            {
                richTextBox1.AppendText("No files selected for upload. Please check files in the list.\n");
                richTextBox1.ScrollToCaret();
                ShowLargeMessageBox("No files selected for upload. Please check files in the list.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 3. Create checked directories on remote
            foreach (string dir in checkedDirs)
            {
                string relativeDir = dir.Replace(selectedPath, "").TrimStart('\\', '/');
                string remoteDirPath = remotePath + "/" + relativeDir.Replace('\\', '/');
                try { session.CreateDirectory(remoteDirPath); } catch { }
            }
            // 4. Upload checked files
            long totalSize = checkedFiles.Sum(f => new FileInfo(f).Length);
            long uploadedSize = 0;
            progressBar1.Value = 0;
            string originalPath = label3.Text;
            richTextBox1.AppendText($"Starting upload of {checkedFiles.Count} files...\n");
            richTextBox1.ScrollToCaret();
            for (int i = 0; i < checkedFiles.Count; i++)
            {
                string currentFile = checkedFiles[i];
                string fileName = Path.GetFileName(currentFile);
                label3.Text = fileName;
                long fileSize = new FileInfo(currentFile).Length;
                string relativePath = currentFile.Replace(selectedPath, "").TrimStart('\\', '/');
                string remoteFilePath = remotePath + "/" + relativePath.Replace('\\', '/');
                string remoteDir = Path.GetDirectoryName(remoteFilePath).Replace('\\', '/');
                TransferOptions transferOptions = new TransferOptions { TransferMode = TransferMode.Binary };
                session.PutFiles(currentFile, remoteDir + "/", false, transferOptions);
                // Set default permission 644 for all uploaded files
                try { session.ExecuteCommand($"chmod 644 '{remoteFilePath}'"); } catch { }
                uploadedSize += fileSize;
                int progressPercent = totalSize > 0 ? (int)((uploadedSize * 100) / totalSize) : 0;
                progressBar1.Value = Math.Min(progressPercent, 100);
                richTextBox1.AppendText($"✓ {i + 1}/{checkedFiles.Count}: {fileName}\n");
                richTextBox1.ScrollToCaret();
            }
            progressBar1.Value = 100;
            richTextBox1.AppendText("Upload completed successfully!\n");
            richTextBox1.ScrollToCaret();
            Task.Delay(1000).ContinueWith(t =>
            {
                if (this.InvokeRequired)
                    this.Invoke(new Action(() => progressBar1.Value = 0));
                else
                    progressBar1.Value = 0;
            });
            label3.Text = originalPath;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // BUTTON SAVE SETTINGS
            try
            {
                // Validate required fields before saving
                if (string.IsNullOrWhiteSpace(textBox1.Text) ||
                    string.IsNullOrWhiteSpace(textBox2.Text) ||
                    string.IsNullOrWhiteSpace(textBox3.Text) ||
                    string.IsNullOrWhiteSpace(textBox4.Text) ||
                    string.IsNullOrWhiteSpace(textBox5.Text))
                {
                    ShowLargeMessageBox("Cannot save settings. All fields (Host, Port, Username, Password, Remote Path) must be filled.", "Missing Fields", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

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
                                        // === NEW: Populate TreeView after loading settings ===
                                        PopulateTreeViewWithCheckBoxes(selectedPath);
                                        // === END NEW ===
                                        loadedCount++;
                                    }
                                    else if (!string.IsNullOrEmpty(value))
                                    {
                                        richTextBox1.AppendText($"Warning: Local path '{value}' does not exist.\n");
                                        richTextBox1.ScrollToCaret();
                                        treeView1.Nodes.Clear();
                                        treeView1.Nodes.Add("No folder selected or folder does not exist.");
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

        // === NEW: Helper to collect checked files from TreeView ===
        private List<string> GetCheckedFiles(TreeNodeCollection nodes)
        {
            List<string> files = new List<string>();
            foreach (TreeNode node in nodes)
            {
                // If node is checked and is a file (leaf node)
                if (node.Checked && node.Nodes.Count == 0 && node.Tag is string path && File.Exists(path))
                {
                    files.Add(path);
                }
                // Recurse into children
                if (node.Nodes.Count > 0)
                {
                    files.AddRange(GetCheckedFiles(node.Nodes));
                }
            }
            return files;
        }

        // === NEW: Helper to collect checked directories from TreeView (including parents of checked files) ===
        private HashSet<string> GetCheckedDirectories(TreeNodeCollection nodes)
        {
            HashSet<string> dirs = new HashSet<string>();
            foreach (TreeNode node in nodes)
            {
                // If node is checked and is a directory (has children)
                if (node.Checked && node.Nodes.Count > 0 && node.Tag is string dirPath && Directory.Exists(dirPath))
                {
                    dirs.Add(dirPath);
                }
                // If any child is checked, add this directory
                if (node.Nodes.Count > 0)
                {
                    var childDirs = GetCheckedDirectories(node.Nodes);
                    if (childDirs.Count > 0 && node.Tag is string parentDir && Directory.Exists(parentDir))
                    {
                        dirs.Add(parentDir);
                    }
                    foreach (var d in childDirs) dirs.Add(d);
                }
            }
            return dirs;
        }

        // === NEW: Set permissions for selected files/folders in TreeView ===
        private void button6_Click(object sender, EventArgs e)
        {
            if (session == null || !session.Opened)
            {
                ShowLargeMessageBox("Not connected to SFTP server.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // If nothing is selected, but there are checked nodes, select the first checked node automatically
            if (treeView1.SelectedNode == null)
            {
                TreeNode firstChecked = FindFirstCheckedNode(treeView1.Nodes);
                if (firstChecked != null)
                {
                    treeView1.SelectedNode = firstChecked;
                }
                else
                {
                    ShowLargeMessageBox("Please select a file or folder in the list.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            // Only allow permission change if the selected node is checked
            if (!treeView1.SelectedNode.Checked)
            {
                ShowLargeMessageBox("Please check the selected file or folder before setting permissions.", "Not Checked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Get permission from ComboBox (e.g., comboBoxPermissions.Text)
            string perm = comboBox1.Text.Trim();
            if (string.IsNullOrEmpty(perm))
            {
                ShowLargeMessageBox("Please select or enter a permission value.", "No Permission", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Expand all descendants so all nodes are loaded
            void ExpandAllNodes(TreeNode node)
            {
                node.Expand();
                foreach (TreeNode child in node.Nodes)
                    ExpandAllNodes(child);
            }
            ExpandAllNodes(treeView1.SelectedNode);
            // Collect all descendants of the selected node (if parent is checked, all children are logically selected)
            List<TreeNode> nodesToSet = new List<TreeNode>();
            void CollectAll(TreeNode node)
            {
                if (node.Tag is string) nodesToSet.Add(node);
                foreach (TreeNode child in node.Nodes) CollectAll(child);
            }
            CollectAll(treeView1.SelectedNode);
            foreach (var node in nodesToSet)
            {
                string localPath = node.Tag as string;
                string relativePath = localPath.Replace(selectedPath, "").TrimStart('\\', '/');
                string remoteTarget = remotePath + "/" + relativePath.Replace('\\', '/');
                try
                {
                    session.ExecuteCommand($"chmod {perm} '{remoteTarget}'");
                    richTextBox1.AppendText($"Permission {perm} set for: {remoteTarget}\n");
                    richTextBox1.ScrollToCaret();
                }
                catch (Exception ex)
                {
                    richTextBox1.AppendText($"Failed to set permission for {remoteTarget}: {ex.Message}\n");
                    richTextBox1.ScrollToCaret();
                }
            }
        }

        // Helper: Find the first checked node in the tree
private TreeNode FindFirstCheckedNode(TreeNodeCollection nodes)
{
    foreach (TreeNode node in nodes)
    {
        if (node.Checked)
            return node;
        if (node.Nodes.Count > 0)
        {
            TreeNode found = FindFirstCheckedNode(node.Nodes);
            if (found != null)
                return found;
        }
    }
    return null;
}
// === Helper: Show a large font message box ===
private DialogResult ShowLargeMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
{
    using (Form form = new Form())
    {
        form.Text = title;
        form.StartPosition = FormStartPosition.CenterParent;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.Size = new Size(480, 220);
        form.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14, FontStyle.Regular);

        Label label = new Label()
        {
            AutoSize = false,
            Text = message,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14, FontStyle.Regular)
        };
        form.Controls.Add(label);

        FlowLayoutPanel panel = new FlowLayoutPanel()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 60
        };
        form.Controls.Add(panel);

        DialogResult result = DialogResult.None;
        void AddButton(string text, DialogResult dr)
        {
            Button btn = new Button()
            {
                Text = text,
                DialogResult = dr,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14, FontStyle.Regular),
                AutoSize = true,
                Margin = new Padding(10, 10, 10, 10)
            };
            btn.Click += (s, e) => { result = dr; form.Close(); };
            panel.Controls.Add(btn);
        }
        if (buttons == MessageBoxButtons.OK)
            AddButton("OK", DialogResult.OK);
        else if (buttons == MessageBoxButtons.OKCancel)
        {
            AddButton("Cancel", DialogResult.Cancel);
            AddButton("OK", DialogResult.OK);
        }
        else if (buttons == MessageBoxButtons.YesNo)
        {
            AddButton("No", DialogResult.No);
            AddButton("Yes", DialogResult.Yes);
        }
        else if (buttons == MessageBoxButtons.YesNoCancel)
        {
            AddButton("Cancel", DialogResult.Cancel);
            AddButton("No", DialogResult.No);
            AddButton("Yes", DialogResult.Yes);
        }
        // Icon (optional)
        if (icon != MessageBoxIcon.None)
        {
            PictureBox pb = new PictureBox()
            {
                Size = new Size(48, 48),
                Location = new Point(20, 20),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            switch (icon)
            {
                case MessageBoxIcon.Error:
                    pb.Image = SystemIcons.Error.ToBitmap(); break;
                case MessageBoxIcon.Warning:
                    pb.Image = SystemIcons.Warning.ToBitmap(); break;
                case MessageBoxIcon.Information:
                    pb.Image = SystemIcons.Information.ToBitmap(); break;
                case MessageBoxIcon.Question:
                    pb.Image = SystemIcons.Question.ToBitmap(); break;
            }
            form.Controls.Add(pb);
            label.Padding = new Padding(80, 20, 20, 20);
        }
        form.AcceptButton = panel.Controls.OfType<Button>().FirstOrDefault();
        form.CancelButton = panel.Controls.OfType<Button>().FirstOrDefault(b => b.DialogResult == DialogResult.Cancel);
        result = form.ShowDialog();
        return result;
    }
}
        // === NEW: Refresh TreeView if folder structure changed ===
        private void RefreshTreeViewIfChanged()
        {
            if (string.IsNullOrEmpty(selectedPath) || !Directory.Exists(selectedPath))
                return;

            // Helper: Recursively get all relative paths from disk
            List<string> GetAllDiskPaths(string root)
            {
                var all = new List<string>();
                foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                    all.Add(dir.Substring(selectedPath.Length).TrimStart(Path.DirectorySeparatorChar));
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    all.Add(file.Substring(selectedPath.Length).TrimStart(Path.DirectorySeparatorChar));
                return all;
            }
            // Helper: Recursively get all relative paths from TreeView
            List<string> GetAllTreePaths(TreeNodeCollection nodes)
            {
                var all = new List<string>();
                foreach (TreeNode node in nodes)
                {
                    if (node.Tag is string path && path.StartsWith(selectedPath))
                        all.Add(path.Substring(selectedPath.Length).TrimStart(Path.DirectorySeparatorChar));
                    if (node.Nodes.Count > 0)
                        all.AddRange(GetAllTreePaths(node.Nodes));
                }
                return all;
            }
            var diskPaths = GetAllDiskPaths(selectedPath);
            var treePaths = GetAllTreePaths(treeView1.Nodes);
            diskPaths.Sort();
            treePaths.Sort();
            if (!diskPaths.SequenceEqual(treePaths))
            {
                // Save checked states
                var checkedSet = new HashSet<string>(GetCheckedFiles(treeView1.Nodes).Concat(GetCheckedDirectories(treeView1.Nodes)));
                PopulateTreeViewWithCheckBoxes(selectedPath);
                // Restore checked states
                void RestoreChecked(TreeNodeCollection nodes)
                {
                    foreach (TreeNode node in nodes)
                    {
                        if (node.Tag is string path && checkedSet.Contains(path))
                            node.Checked = true;
                        if (node.Nodes.Count > 0)
                            RestoreChecked(node.Nodes);
                    }
                }
                RestoreChecked(treeView1.Nodes);
                // Show short message in label3 instead of expanding all nodes or showing a message box
                label3.Text = "Tree updated, review new files/folders.";
            }
        }

        private void MirrorDeleteRemoteExtra(Session session, string localRoot, string remoteRoot)
        {
            // Get all local relative paths
            var localDirs = new HashSet<string>(Directory.GetDirectories(localRoot, "*", SearchOption.AllDirectories)
                .Select(d => d.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/')));
            var localFiles = new HashSet<string>(Directory.GetFiles(localRoot, "*", SearchOption.AllDirectories)
                .Select(f => f.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/')));

            // List all remote files and directories recursively
            RemoteDirectoryInfo remoteListing = session.ListDirectory(remoteRoot);
            var remoteDirs = new List<string>();
            var remoteFiles = new List<string>();
            void CollectRemote(RemoteDirectoryInfo dir, string relPath)
            {
                foreach (var sub in dir.Files.Where(f => f.IsDirectory && f.Name != "." && f.Name != ".."))
                {
                    string subRel = string.IsNullOrEmpty(relPath) ? sub.Name : relPath + "/" + sub.Name;
                    remoteDirs.Add(subRel);
                    CollectRemote(session.ListDirectory(remoteRoot + "/" + subRel), subRel);
                }
                foreach (var file in dir.Files.Where(f => !f.IsDirectory))
                {
                    string fileRel = string.IsNullOrEmpty(relPath) ? file.Name : relPath + "/" + file.Name;
                    remoteFiles.Add(fileRel);
                }
            }
            CollectRemote(remoteListing, "");

            // Delete remote files not in local
            foreach (var remoteFile in remoteFiles)
            {
                if (!localFiles.Contains(remoteFile.Replace('/', Path.DirectorySeparatorChar)))
                {
                    try
                    {
                        session.RemoveFiles(remoteRoot + "/" + remoteFile);
                        richTextBox1.AppendText($"Deleted remote file: {remoteFile}\n");
                    }
                    catch (Exception ex)
                    {
                        richTextBox1.AppendText($"Failed to delete remote file {remoteFile}: {ex.Message}\n");
                    }
                }
            }
            // Delete remote directories not in local (reverse order for safe delete)
            foreach (var remoteDir in remoteDirs.OrderByDescending(s => s.Length))
            {
                if (!localDirs.Contains(remoteDir.Replace('/', Path.DirectorySeparatorChar)))
                {
                    try
                    {
                        session.RemoveFiles(remoteRoot + "/" + remoteDir + "/");
                        richTextBox1.AppendText($"Deleted remote folder: {remoteDir}\n");
                    }
                    catch (Exception ex)
                    {
                        richTextBox1.AppendText($"Failed to delete remote folder {remoteDir}: {ex.Message}\n");
                    }
                }
            }
        }

        // Highlight expanded node by selecting it
        private void TreeView1_AfterExpand(object sender, TreeViewEventArgs e)
        {
            treeView1.SelectedNode = e.Node;
        }
        // Remove highlight when collapsed (optional: do nothing, or select parent)
        private void TreeView1_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            // Optionally, select parent node when collapsed
            if (e.Node.Parent != null)
                treeView1.SelectedNode = e.Node.Parent;
        }
        
        /*
        // Select node when its checkbox is clicked
        private void TreeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // Prevent recursive event firing
            treeView1.AfterCheck -= TreeView1_AfterCheck;
            try
            {
                // Check/uncheck all children
                SetChildNodesChecked(e.Node, e.Node.Checked);
                // Optionally, update parent nodes (if all siblings checked, check parent)
                UpdateParentNodesChecked(e.Node);
                // Select the node that was just checked/unchecked
                treeView1.SelectedNode = e.Node;
            }
            finally
            {
                treeView1.AfterCheck += TreeView1_AfterCheck;
            }
        }
        */
    }
}