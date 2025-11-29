using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using Seagull.BarTender.Print;

namespace smart_label
{
    public partial class Form1 : Form
    {
        private const string IniFileName = "config.ini";
        private string templatesFolderPath = string.Empty;
        private string selectedTemplatePath = string.Empty;
        private Dictionary<int, string> dataSourceNames = new Dictionary<int, string>();
        private Dictionary<int, string> dataSourceFields = new Dictionary<int, string>();
        private int previousCount = 0;
        private bool suppressCountPrompt = false;

        // helper item for combo box
        private class TemplateItem
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public TemplateItem(string name, string fullPath)
            {
                Name = name;
                FullPath = fullPath;
            }
            public override string ToString() => Name;
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Determine config path
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IniFileName);
            if (File.Exists(path))
            {
                LoadConfig(path);
            }
            else
            {
                // create default inputs
                CreateInputControls((int)numericUpDownCount.Value);
                // default templates folder under application directory: \templates
                templatesFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
                try
                {
                    if (!Directory.Exists(templatesFolderPath)) Directory.CreateDirectory(templatesFolderPath);
                }
                catch { }
                PopulateTemplateList(templatesFolderPath);
            }
            previousCount = (int)numericUpDownCount.Value;
        }

        private void numericUpDownCount_ValueChanged(object sender, EventArgs e)
        {
            if (suppressCountPrompt) return;
             int newCount = (int)numericUpDownCount.Value;
             if (newCount > previousCount)
             {
                 // prompt user to provide display name and field mapping for each newly added data source
                 for (int i = previousCount + 1; i <= newCount; i++)
                 {
                     var cfg = PromptForDataSourceConfig(i);
                     if (cfg == null)
                     {
                         // user cancelled: revert to previousCount
                         numericUpDownCount.Value = previousCount;
                         return;
                     }
                     if (string.IsNullOrWhiteSpace(cfg.Name)) cfg.Name = $"数据源 {i}";
                     if (string.IsNullOrWhiteSpace(cfg.Field)) cfg.Field = "DS" + i;
                     dataSourceNames[i] = cfg.Name;
                     dataSourceFields[i] = cfg.Field;
                 }
             }
             else if (newCount < previousCount)
             {
                 // remove names beyond newCount
                 for (int i = newCount + 1; i <= previousCount; i++)
                 {
                     if (dataSourceNames.ContainsKey(i)) dataSourceNames.Remove(i);
                     if (dataSourceFields.ContainsKey(i)) dataSourceFields.Remove(i);
                 }
             }

             CreateInputControls(newCount);
             previousCount = newCount;
         }

        // Data source configuration with Display Name and Field mapping
        private class DataSourceConfig
        {
            public string Name { get; set; }
            public string Field { get; set; }
        }

        // Prompt user to input data source display name and mapped field. Returns config or null if cancelled.
        private DataSourceConfig PromptForDataSourceConfig(int index)
        {
            using (var f = new Form())
            {
                f.Text = "配置数据源";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(420, 160);
                f.MinimizeBox = false;
                f.MaximizeBox = false;

                var lblName = new Label() { Left = 10, Top = 10, Width = 390, Text = $"第 {index} 个数据源 显示名称：" };
                var tbName = new TextBox() { Left = 10, Top = 30, Width = 390 };

                var lblField = new Label() { Left = 10, Top = 60, Width = 390, Text = "映射到模板的字段名（例如 DS1 或 FieldName）：" };
                var tbField = new TextBox() { Left = 10, Top = 80, Width = 390 };

                var btnOk = new Button() { Text = "确定", Left = 240, Width = 80, Top = 110, DialogResult = DialogResult.OK };
                var btnCancel = new Button() { Text = "取消", Left = 330, Width = 80, Top = 110, DialogResult = DialogResult.Cancel };

                f.Controls.Add(lblName);
                f.Controls.Add(tbName);
                f.Controls.Add(lblField);
                f.Controls.Add(tbField);
                f.Controls.Add(btnOk);
                f.Controls.Add(btnCancel);

                f.AcceptButton = btnOk;
                f.CancelButton = btnCancel;

                var dr = f.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    return new DataSourceConfig { Name = tbName.Text ?? string.Empty, Field = tbField.Text ?? string.Empty };
                }
                return null;
            }
        }

        private void CreateInputControls(int count)
        {
            panelInputs.Controls.Clear();
            panelInputs.SuspendLayout();
            int top = 10;
            for (int i = 1; i <= count; i++)
            {
                var lbl = new Label();
                string labelName = dataSourceNames.ContainsKey(i) ? dataSourceNames[i] : $"数据源 {i}";
                lbl.Text = labelName + ":";
                lbl.AutoSize = true;
                lbl.Location = new Point(10, top + 3);

                var txt = new TextBox();
                txt.Name = GetTextBoxName(i);
                // make textbox stretch to panel width
                txt.Location = new Point(90, top);
                txt.Size = new Size(Math.Max(100, panelInputs.ClientSize.Width - 110), 21);
                txt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                txt.KeyDown += DataSourceTextBox_KeyDown;

                panelInputs.Controls.Add(lbl);
                panelInputs.Controls.Add(txt);

                top += 35;
            }
            panelInputs.ResumeLayout();
        }

        // When Enter is pressed in a data source textbox, move focus to the next textbox.
        // If it's the last textbox, trigger printing, then clear inputs and focus the first textbox.
        private async void DataSourceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // prevent ding
                var tb = sender as TextBox;
                if (tb == null) return;
                // Expect name format txtDataSource_N
                var parts = tb.Name.Split('_');
                if (parts.Length < 2) return;
                if (!int.TryParse(parts[1], out int idx)) return;
                int next = idx + 1;
                var nextCtrl = panelInputs.Controls.Find(GetTextBoxName(next), true).FirstOrDefault() as TextBox;
                if (nextCtrl != null)
                {
                    nextCtrl.Focus();
                    nextCtrl.SelectAll();
                }
                else
                {
                    // last textbox: start print
                    bool ok = await PrintSelectedTemplateAsync();
                    if (ok)
                    {
                        ClearInputsAndFocusFirst();
                    }
                }
            }
        }

        // Performs the print operation asynchronously. Returns true if printing succeeded (or was requested), false otherwise.
        private async Task<bool> PrintSelectedTemplateAsync()
        {
            if (string.IsNullOrWhiteSpace(selectedTemplatePath) || !File.Exists(selectedTemplatePath))
            {
                MessageBox.Show("请先选择有效的模板文件 (.btw)");
                return false;
            }

            btnPrintTemplate.Enabled = false;
            try
            {
                await Task.Run(() => PrintTemplate(selectedTemplatePath));
                MessageBox.Show("打印请求已发送");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("打印失败: " + ex.Message);
                return false;
            }
            finally
            {
                btnPrintTemplate.Enabled = true;
            }
        }

        private void ClearInputsAndFocusFirst()
        {
            // Clear all data source textboxes
            var first = panelInputs.Controls.Find(GetTextBoxName(1), true).FirstOrDefault() as TextBox;
            foreach (Control c in panelInputs.Controls)
            {
                if (c is TextBox tb && tb.Name.StartsWith("txtDataSource_"))
                {
                    tb.Text = string.Empty;
                }
            }
            // focus first if exists
            if (first != null)
            {
                first.Focus();
            }
        }

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            // require password before saving
            const string requiredPassword = "20251129";
            string entered = PromptForPassword();
            if (entered == null)
            {
                // user cancelled
                return;
            }
            if (entered != requiredPassword)
            {
                MessageBox.Show("密码错误，配置未保存");
                return;
            }

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IniFileName);
            SaveConfig(path);
            MessageBox.Show("配置已保存: " + path);
        }

        // Shows a simple modal password prompt. Returns entered password or null if cancelled.
        private string PromptForPassword()
        {
            using (var f = new Form())
            {
                f.Text = "请输入保存密码";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(320, 110);
                f.MinimizeBox = false;
                f.MaximizeBox = false;

                var lbl = new Label() { Left = 10, Top = 10, Width = 300, Text = "请输入密码以保存配置：" };
                var tb = new TextBox() { Left = 10, Top = 35, Width = 300, UseSystemPasswordChar = true };
                var btnOk = new Button() { Text = "确定", Left = 150, Width = 75, Top = 70, DialogResult = DialogResult.OK };
                var btnCancel = new Button() { Text = "取消", Left = 235, Width = 75, Top = 70, DialogResult = DialogResult.Cancel };

                f.Controls.Add(lbl);
                f.Controls.Add(tb);
                f.Controls.Add(btnOk);
                f.Controls.Add(btnCancel);

                f.AcceptButton = btnOk;
                f.CancelButton = btnCancel;

                var dr = f.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    return tb.Text ?? string.Empty;
                }
                return null;
            }
        }

        private void btnLoadConfig_Click(object sender, EventArgs e)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IniFileName);
            if (File.Exists(path))
            {
                LoadConfig(path);
                MessageBox.Show("配置已加载");
            }
            else
            {
                MessageBox.Show("未找到配置文件: " + path);
            }
        }

        private void SaveConfig(string path)
        {
            // Write count and each data source value
            IniWriteValue("General", "Count", numericUpDownCount.Value.ToString(), path);
            IniWriteValue("General", "TemplatesFolder", templatesFolderPath ?? string.Empty, path);
            for (int i = 1; i <= (int)numericUpDownCount.Value; i++)
            {
                 var tb = panelInputs.Controls.Find(GetTextBoxName(i), true).FirstOrDefault() as TextBox;
                 string val = tb?.Text ?? string.Empty;
                 IniWriteValue("DataSource" + i, "Value", val, path);
                string name = dataSourceNames.ContainsKey(i) ? dataSourceNames[i] : string.Empty;
                IniWriteValue("DataSource" + i, "Name", name, path);
                string field = dataSourceFields.ContainsKey(i) ? dataSourceFields[i] : string.Empty;
                IniWriteValue("DataSource" + i, "Field", field, path);
             }
        }

        private void LoadConfig(string path)
        {
            string countStr = IniReadValue("General", "Count", path);
            int count = 1;
            if (!int.TryParse(countStr, out count) || count < 0) count = 0;
            if (count > numericUpDownCount.Maximum) count = (int)numericUpDownCount.Maximum;

            // load names and field mappings for data sources first
            dataSourceNames.Clear();
            dataSourceFields.Clear();
            for (int i = 1; i <= count; i++)
            {
                string name = IniReadValue("DataSource" + i, "Name", path);
                if (!string.IsNullOrWhiteSpace(name)) dataSourceNames[i] = name;
                string field = IniReadValue("DataSource" + i, "Field", path);
                if (!string.IsNullOrWhiteSpace(field)) dataSourceFields[i] = field;
            }

            // set count without triggering prompt
            suppressCountPrompt = true;
            try
            {
                numericUpDownCount.Value = count;
            }
            finally
            {
                suppressCountPrompt = false;
            }

            templatesFolderPath = IniReadValue("General", "TemplatesFolder", path);
            if (string.IsNullOrWhiteSpace(templatesFolderPath))
            {
                templatesFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
                try { if (!Directory.Exists(templatesFolderPath)) Directory.CreateDirectory(templatesFolderPath); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(templatesFolderPath) && Directory.Exists(templatesFolderPath))
            {
                PopulateTemplateList(templatesFolderPath);
            }

            CreateInputControls(count);
            for (int i = 1; i <= count; i++)
            {
                string val = IniReadValue("DataSource" + i, "Value", path);
                var tb = panelInputs.Controls.Find(GetTextBoxName(i), true).FirstOrDefault() as TextBox;
                if (tb != null) tb.Text = val;
            }
            previousCount = count;
        }

        // Populate the combo box with .btw templates from the specified folder
        private void PopulateTemplateList(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                comboBoxTemplates.Items.Clear();
                lblSelectedTemplate.Text = "未找到 .btw 模板";
                selectedTemplatePath = string.Empty;
                return;
            }

            comboBoxTemplates.Items.Clear();
            try
            {
                var files = Directory.GetFiles(folder, "*.btw");
                foreach (var f in files)
                {
                    comboBoxTemplates.Items.Add(new TemplateItem(Path.GetFileName(f), f));
                }
                if (files.Length == 0)
                {
                    lblSelectedTemplate.Text = "未找到 .btw 模板";
                    selectedTemplatePath = string.Empty;
                }
                else
                {
                    comboBoxTemplates.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取模板文件夹出错: " + ex.Message);
            }
        }

        private void comboBoxTemplates_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = comboBoxTemplates.SelectedItem as TemplateItem;
            if (item != null)
            {
                selectedTemplatePath = item.FullPath;
                lblSelectedTemplate.Text = item.Name;
                LoadTemplatePreview(selectedTemplatePath);
            }
            else
            {
                selectedTemplatePath = string.Empty;
                lblSelectedTemplate.Text = "未选择模板文件";
                pictureBoxPreview.Image = null;
            }
        }

        private void LoadTemplatePreview(string templatePath)
        {
            pictureBoxPreview.Image = null; // clear existing image
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
                return;

            Engine btEngine = null;
            try
            {
                btEngine = new Engine(true);
                // use dynamic to simplify SDK member access at runtime
                LabelFormatDocument doc = btEngine.Documents.Open(templatePath);

                // ExportImage method available?
                try
                {
                    string imgPath = Path.ChangeExtension(templatePath, ".png");
                    doc.ExportImageToFile(imgPath, ImageType.BMP, Seagull.BarTender.Print.ColorDepth.ColorDepth256, new Resolution(700, 300
                    ), OverwriteOptions.Overwrite);

                    // Load the exported image
                    if (File.Exists(imgPath))
                    {
                        pictureBoxPreview.Image = new Bitmap(imgPath);
                    }
                }
                catch { }

                // Close via reflection to avoid compile-time enum dependency
                try
                {
                    var closeMethod = ((object)doc).GetType().GetMethod("Close");
                    if (closeMethod != null)
                    {
                        var parms = closeMethod.GetParameters();
                        if (parms != null && parms.Length == 1)
                        {
                            var enumType = parms[0].ParameterType;
                            var dontSaveValue = Enum.ToObject(enumType, 0);
                            closeMethod.Invoke(((object)doc), new object[] { dontSaveValue });
                        }
                        else
                        {
                            closeMethod.Invoke(((object)doc), null);
                        }
                    }
                }
                catch { }
            }
            finally
            {
                if (btEngine != null)
                {
                    try { btEngine.Stop(); } catch { }
                }
            }
        }

        private string GetTextBoxName(int index) => $"txtDataSource_{index}";

        private async void btnPrintTemplate_Click(object sender, EventArgs e)
        {
            bool ok = await PrintSelectedTemplateAsync();
            if (ok)
            {
                ClearInputsAndFocusFirst();
            }
        }

        private void PrintTemplate(string templatePath)
        {
            Engine btEngine = null;
            try
            {
                btEngine = new Engine(true);
                LabelFormatDocument doc = btEngine.Documents.Open(templatePath);

                // collect missing fields
                var missingFields = new List<string>();

                // set named substrings DS1, DS2, ... from inputs
                for (int i = 1; i <= (int)numericUpDownCount.Value; i++)
                {
                    var tb = panelInputs.Controls.Find(GetTextBoxName(i), true).FirstOrDefault() as TextBox;
                    if (tb == null) continue;
                    string value = tb.Text ?? string.Empty;

                    // prefer configured field mapping if exists, else fall back to DS{i}
                    string field = dataSourceFields.ContainsKey(i) && !string.IsNullOrWhiteSpace(dataSourceFields[i]) ? dataSourceFields[i] : ("DS" + i);

                    try
                    {
                        // access named substring; will throw or be null if not present
                        var subStrings = doc.SubStrings;
                        var named = subStrings[field];
                        if (named != null)
                        {
                            named.Value = value;
                        }
                        else
                        {
                            missingFields.Add(field);
                        }
                    }
                    catch
                    {
                        // record missing and continue
                        missingFields.Add(field);
                    }
                }

                if (missingFields.Count > 0)
                {
                    // throw with a clear message so the async wrapper can show it in UI thread
                    var unique = string.Join(", ", missingFields.Distinct());
                    throw new InvalidOperationException("模板中未找到以下字段: " + unique);
                }

                try
                {
                    doc.PrintSetup.IdenticalCopiesOfLabel = 1;
                }
                catch { }

                try
                {
                    doc.Print();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("BarTender PrintOut failed: " + ex.Message, ex);
                }

                // Close via reflection to avoid compile-time enum dependency
                try
                {
                    var closeMethod = ((object)doc).GetType().GetMethod("Close");
                    if (closeMethod != null)
                    {
                        var parms = closeMethod.GetParameters();
                        if (parms != null && parms.Length == 1)
                        {
                            var enumType = parms[0].ParameterType;
                            var dontSaveValue = Enum.ToObject(enumType, 0);
                            closeMethod.Invoke(((object)doc), new object[] { dontSaveValue });
                        }
                        else
                        {
                            closeMethod.Invoke(((object)doc), null);
                        }
                    }
                }
                catch { }
            }
            finally
            {
                if (btEngine != null)
                {
                    try { btEngine.Stop(); } catch { }
                }
            }
        }

        // Simple INI helpers using WinAPI
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        private void IniWriteValue(string section, string key, string value, string filePath)
        {
            WritePrivateProfileString(section, key, value, filePath);
        }

        private string IniReadValue(string section, string key, string filePath)
        {
            StringBuilder sb = new StringBuilder(2048);
            GetPrivateProfileString(section, key, string.Empty, sb, sb.Capacity, filePath);
            return sb.ToString();
        }
    }
}
