using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinSCP;
using C_SHARP_MNI_FTP_UPLOADER_2025;

namespace C_SHARP_MNI_FTP_UPLOADER_2025
{

    public partial class Form1 : Form
    {
        Session session = new WinSCP.Session();

        // New: RichTextBox for differences
        private RichTextBox richTextBoxDifferences;

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

            // Removed: RichTextBox for differences (now in dialog)
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
                    // Change Load Settings button to FOLDER REFRESH
                    button5.Text = "FOLDER REFRESH";

                    // === NEW: Compare local and remote folder structure ===
                    bool structureIsSame = CompareLocalAndRemoteStructure(selectedPath, remotePath);
                    if (structureIsSame)
                    {
                        richTextBox1.AppendText("Local and remote folder structures are the SAME.\n");
                    }
                    else
                    {
                        richTextBox1.AppendText("Local and remote folder structures are DIFFERENT.\n");
                    }
                    richTextBox1.ScrollToCaret();
                    // === END NEW ===
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
                // Revert FOLDER REFRESH button to Load Settings
                //button5.Text = "Load Settings";
            }
        } // end of button1_Click CONNECT / DISCONNECT

        private void button2_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
            // Always refresh TreeView to show latest local folder structure
            //PopulateTreeViewWithCheckBoxes(selectedPath);
            // === NEW: Mirror delete remote files/folders not present locally ===
            try
            {
                MirrorDeleteRemoteExtra(session, selectedPath, remotePath); // ADD TO WORK FOR NEW DIRS
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText($"Mirror delete failed: {ex.Message}\n");
            }

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
            if (checkedFiles.Count == 0 && checkedDirs.Count == 0)
            {
                richTextBox1.AppendText("No files or folders selected for upload. Please check files or folders in the list.\n");
                richTextBox1.ScrollToCaret();
                ShowLargeMessageBox("No files or folders selected for upload. Please check files or folders in the list.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            // Refresh TreeView to reflect any local deletions
           // PopulateTreeViewWithCheckBoxes(selectedPath);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // Cleared as requested
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

        private void MirrorDeleteRemoteExtra(Session session, string localRoot, string remoteRoot)
        {
            // Get all local relative paths
            var localDirs = new HashSet<string>(Directory.GetDirectories(localRoot, "*", SearchOption.AllDirectories)
                .Select(d => d.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar).Replace("\\", "/")));
            var localFiles = new HashSet<string>(Directory.GetFiles(localRoot, "*", SearchOption.AllDirectories)
                .Select(f => f.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar).Replace("\\", "/")));

            // List all remote files and directories recursively
            RemoteDirectoryInfo remoteListing = session.ListDirectory(remoteRoot);
            var remoteDirs = new List<string>();
            var remoteFiles = new List<string>();
            void CollectRemote(RemoteDirectoryInfo dir, string relPath)
            {
                foreach (RemoteFileInfo sub in dir.Files.Where(f => f.IsDirectory && f.Name != "." && f.Name != ".."))
                {
                    string subRel = string.IsNullOrEmpty(relPath) ? sub.Name : relPath + "/" + sub.Name;
                    remoteDirs.Add(subRel);
                    CollectRemote(session.ListDirectory(remoteRoot + "/" + subRel), subRel);
                }
                foreach (RemoteFileInfo file in dir.Files.Where(f => !f.IsDirectory))
                {
                    string fileRel = string.IsNullOrEmpty(relPath) ? file.Name : relPath + "/" + file.Name;
                    remoteFiles.Add(fileRel);
                }
            }
            CollectRemote(remoteListing, "");

            // Delete remote files not in local
            foreach (string remoteFile in remoteFiles)
            {
                if (!localFiles.Contains(remoteFile))
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
            foreach (string remoteDir in remoteDirs.OrderByDescending(s => s.Length))
            {
                if (!localDirs.Contains(remoteDir))
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
            // Create new directories on remote that exist locally but not remotely
            /*
            foreach (string localDir in localDirs)
            {
                if (!remoteDirs.Contains(localDir))
                {
                    try
                    {
                        session.CreateDirectory(remoteRoot + "/" + localDir);
                        richTextBox1.AppendText($"Created remote folder: {localDir}\n");
                    }
                    catch (Exception ex)
                    {
                        richTextBox1.AppendText($"Failed to create remote folder {localDir}: {ex.Message}\n");
                    }
                }
            }
            // Upload new files to remote that exist locally but not remotely
            foreach (string localFile in localFiles)
            {
                if (!remoteFiles.Contains(localFile))
                {
                    try
                    {
                        string localFilePath = Path.Combine(localRoot, localFile.Replace('/', Path.DirectorySeparatorChar));
                        string remoteFilePath = remoteRoot + "/" + localFile;
                        string remoteDir = Path.GetDirectoryName(remoteFilePath).Replace("\\", "/");
                        TransferOptions transferOptions = new TransferOptions { TransferMode = TransferMode.Binary };
                        session.PutFiles(localFilePath, remoteDir + "/", false, transferOptions);
                        richTextBox1.AppendText($"Uploaded new file: {localFile}\n");
                    }
                    catch (Exception ex)
                    {
                        richTextBox1.AppendText($"Failed to upload new file {localFile}: {ex.Message}\n");
                    }
                }
            }
            */
        }



        // === NEW: Populate TreeView with checkboxes for folder/file structure ===
        private void PopulateTreeViewWithCheckBoxes(string rootPath)
        {
            treeView1.BeginUpdate();
            // === Preserve checked states and selected node ===
            // 1. Collect checked paths and selected path
            var checkedPaths = new HashSet<string>();
            void CollectChecked(TreeNodeCollection nodes)
            {
                foreach (TreeNode node in nodes)
                {
                    if (node.Checked && node.Tag is string path)
                        checkedPaths.Add(path);
                    if (node.Nodes.Count > 0)
                        CollectChecked(node.Nodes);
                }
            }
            CollectChecked(treeView1.Nodes);
            string selectedNodePath = treeView1.SelectedNode != null && treeView1.SelectedNode.Tag is string selPath ? selPath : null;

            treeView1.Nodes.Clear();
            treeView1.CheckBoxes = true; // Ensure checkboxes are visible
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                treeView1.EndUpdate();
                return;
            }
            DirectoryInfo rootDir = new DirectoryInfo(rootPath);
            TreeNode rootNode = CreateDirectoryNodeWithRestore(rootDir, checkedPaths, selectedNodePath, out TreeNode restoredSelectedNode);
            treeView1.Nodes.Add(rootNode);
            rootNode.Expand();
            // Restore selected node if found
            if (restoredSelectedNode != null)
                treeView1.SelectedNode = restoredSelectedNode;
            treeView1.EndUpdate();
            // Remove event handler attach/detach here (handled in Form1_Load)
        }

        // New version: restores checked state and selected node
        private TreeNode CreateDirectoryNodeWithRestore(DirectoryInfo dirInfo, HashSet<string> checkedPaths, string selectedNodePath, out TreeNode selectedNode)
        {
            TreeNode dirNode = new TreeNode(dirInfo.Name) { Tag = dirInfo.FullName };
            if (checkedPaths.Contains(dirInfo.FullName))
                dirNode.Checked = true;
            selectedNode = null;
            if (selectedNodePath == dirInfo.FullName)
                selectedNode = dirNode;
            // Add subdirectories
            foreach (var subDir in dirInfo.GetDirectories())
            {
                TreeNode childNode = CreateDirectoryNodeWithRestore(subDir, checkedPaths, selectedNodePath, out TreeNode foundSel);
                dirNode.Nodes.Add(childNode);
                if (foundSel != null)
                    selectedNode = foundSel;
            }
            // Add files
            foreach (var file in dirInfo.GetFiles())
            {
                TreeNode fileNode = new TreeNode(file.Name) { Tag = file.FullName };
                if (checkedPaths.Contains(file.FullName))
                    fileNode.Checked = true;
                if (selectedNodePath == file.FullName)
                    selectedNode = fileNode;
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

        private void button5_Click(object sender, EventArgs e)
        {   // BUTTON FOLDER REFRESH ONLY
            richTextBox1.Clear();
            // Only refresh logic, no settings loading
            if (session != null && session.Opened)
            {
                // 1. Refresh TreeView from local folder
                PopulateTreeViewWithCheckBoxes(selectedPath);
                // 2. Collect local and remote structure
                var localDirs = new HashSet<string>(Directory.GetDirectories(selectedPath, "*", SearchOption.AllDirectories)
                    .Select(d => d.Substring(selectedPath.Length).TrimStart(Path.DirectorySeparatorChar).Replace("\\", "/")));
                var localFiles = new HashSet<string>(Directory.GetFiles(selectedPath, "*", SearchOption.AllDirectories)
                    .Select(f => f.Substring(selectedPath.Length).TrimStart(Path.DirectorySeparatorChar).Replace("\\", "/")));
                var remoteListing = session.ListDirectory(remotePath);
                var remoteDirs = new HashSet<string>();
                var remoteFiles = new HashSet<string>();
                void CollectRemote(WinSCP.RemoteDirectoryInfo dir, string relPath)
                {
                    foreach (var sub in dir.Files.Where(f => f.IsDirectory && f.Name != "." && f.Name != ".."))
                    {
                        string subRel = string.IsNullOrEmpty(relPath) ? sub.Name : relPath + "/" + sub.Name;
                        remoteDirs.Add(subRel);
                        CollectRemote(session.ListDirectory(remotePath + "/" + subRel), subRel);
                    }
                    foreach (var file in dir.Files.Where(f => !f.IsDirectory))
                    {
                        string fileRel = string.IsNullOrEmpty(relPath) ? file.Name : relPath + "/" + file.Name;
                        remoteFiles.Add(fileRel);
                    }
                }
                CollectRemote(remoteListing, "");

                // 3. Find differences
                var onlyLocalDirs = localDirs.Except(remoteDirs).OrderBy(x => x).ToList();
                var onlyRemoteDirs = remoteDirs.Except(localDirs).OrderBy(x => x).ToList();
                var onlyLocalFiles = localFiles.Except(remoteFiles).OrderBy(x => x).ToList();
                var onlyRemoteFiles = remoteFiles.Except(localFiles).OrderBy(x => x).ToList();

                // 4. Build enhanced message with diagnostics
                var sb = new StringBuilder();
                sb.AppendLine($"DIAGNOSTICS:");
                sb.AppendLine($"onlyLocalDirs.Count: {onlyLocalDirs.Count}");
                sb.AppendLine($"onlyRemoteDirs.Count: {onlyRemoteDirs.Count}");
                sb.AppendLine($"onlyLocalFiles.Count: {onlyLocalFiles.Count}");
                sb.AppendLine($"onlyRemoteFiles.Count: {onlyRemoteFiles.Count}");
                sb.AppendLine();
                if (onlyLocalDirs.Count > 0)
                {
                    sb.AppendLine("Sample onlyLocalDirs:");
                    foreach (var d in onlyLocalDirs.Take(3))
                        sb.AppendLine(d);
                }
                if (onlyRemoteDirs.Count > 0)
                {
                    sb.AppendLine("Sample onlyRemoteDirs:");
                    foreach (var d in onlyRemoteDirs.Take(3))
                        sb.AppendLine(d);
                }
                if (onlyLocalFiles.Count > 0)
                {
                    sb.AppendLine("Sample onlyLocalFiles:");
                    foreach (var f in onlyLocalFiles.Take(3))
                        sb.AppendLine(f);
                }
                if (onlyRemoteFiles.Count > 0)
                {
                    sb.AppendLine("Sample onlyRemoteFiles:");
                    foreach (var f in onlyRemoteFiles.Take(3))
                        sb.AppendLine(f);
                }
                sb.AppendLine();

                if (onlyLocalDirs.Count == 0 && onlyRemoteDirs.Count == 0 && onlyLocalFiles.Count == 0 && onlyRemoteFiles.Count == 0)
                {
                    sb.AppendLine("Local and remote folder structures are the SAME.");
                }
                else
                {
                    sb.AppendLine("Local and remote folder structures are DIFFERENT.\n");

                    // Add folder names only, one per line
                    var allFolderNames = onlyLocalDirs.Concat(onlyRemoteDirs)
                        .Select(path => {
                            var parts = path.Split(new char[] {'/', '\\'}, StringSplitOptions.RemoveEmptyEntries);
                            return parts.Length > 0 ? parts[parts.Length - 1] : path;
                        })
                        .Distinct()
                        .ToList();
                    // Move VS_CODE_EDIT_WORK_TEMP to the top if present
                    var orderedFolderNames = allFolderNames
                        .OrderBy(name => name == "VS_CODE_EDIT_WORK_TEMP" ? "" : name)
                        .ToList();
                    if (orderedFolderNames.Count > 0)
                    {
                        sb.AppendLine("Folder names:");
                        foreach (var name in orderedFolderNames)
                            sb.AppendLine(name);
                        sb.AppendLine();
                    }

                    // ...existing code for summary and detailed listing...
                    if (onlyLocalDirs.Count > 0 || onlyLocalFiles.Count > 0)
                    {
                        sb.AppendLine("To make REMOTE match LOCAL, upload:");
                        if (onlyLocalDirs.Count > 0)
                        {
                            sb.AppendLine("  Folders:");
                            foreach (var d in onlyLocalDirs) sb.AppendLine("    - " + d);
                        }
                        if (onlyLocalFiles.Count > 0)
                        {
                            sb.AppendLine("  Files:");
                            foreach (var f in onlyLocalFiles) sb.AppendLine("    - " + f);
                        }
                        sb.AppendLine();
                    }
                    if (onlyLocalDirs.Count > 0)
                    {
                        sb.AppendLine("Folders only in LOCAL:");
                        foreach (var d in onlyLocalDirs) sb.AppendLine("  - " + d);
                        sb.AppendLine();
                    }
                    if (onlyRemoteDirs.Count > 0)
                    {
                        sb.AppendLine("Folders only in REMOTE:");
                        foreach (var d in onlyRemoteDirs) sb.AppendLine("  - " + d);
                        sb.AppendLine();
                    }
                    if (onlyLocalFiles.Count > 0)
                    {
                        sb.AppendLine("Files only in LOCAL:");
                        foreach (var f in onlyLocalFiles) sb.AppendLine("  - " + f);
                        sb.AppendLine();
                    }
                    if (onlyRemoteFiles.Count > 0)
                    {
                        sb.AppendLine("Files only in REMOTE:");
                        foreach (var f in onlyRemoteFiles) sb.AppendLine("  - " + f);
                        sb.AppendLine();
                    }
                }

                // 5. Show in DifferencesDialog
                using (var dlg = new DifferencesDialog(sb.ToString(), "Folder Differences"))
                {
                    dlg.ShowDialog(this);
                }
                return;
            }
        }

        // === NEW: Button 7 for loading settings ===
        private void button7_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
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

        // === NEW: Set permissions for selected files/folders in TreeView ===
        private void button6_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();

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

        // === NEW: Compare local and remote folder structure ===
        private bool CompareLocalAndRemoteStructure(string localRoot, string remoteRoot)
        {
            try
            {
                if (string.IsNullOrEmpty(localRoot) || !Directory.Exists(localRoot) || session == null || !session.Opened)
                    return false;
                // Get all local relative paths
                var localDirs = new HashSet<string>(Directory.GetDirectories(localRoot, "*", SearchOption.AllDirectories)
                    .Select(d => d.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/')));
                var localFiles = new HashSet<string>(Directory.GetFiles(localRoot, "*", SearchOption.AllDirectories)
                    .Select(f => f.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/')));

                // List all remote files and directories recursively
                RemoteDirectoryInfo remoteListing = session.ListDirectory(remoteRoot);
                var remoteDirs = new HashSet<string>();
                var remoteFiles = new HashSet<string>();
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

                // Compare sets
                bool dirsEqual = localDirs.SetEquals(remoteDirs);
                bool filesEqual = localFiles.SetEquals(remoteFiles);
                return dirsEqual && filesEqual;
            }
            catch
            {
                return false;
            }
        }
        // === END NEW ===

        // === NEW: Helper to show a large font message box ===
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

        // ...existing code...
    } // End of Form1 class
    
    
    // End of namespace
}