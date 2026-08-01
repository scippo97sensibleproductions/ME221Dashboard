using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using VirtualDyno.Core;
using VirtualDyno.Core.Resources;
using VirtualDyno.Properties;

namespace VirtualDyno;

public class CarEditor : Form
{
	private IContainer components;

	private TreeView tvCarList;

	private SplitContainer splitContainer1;

	private TextBox txtMake;

	private Label label1;

	private TextBox txtModel;

	private Label label2;

	private TextBox txtSubModel;

	private Label label4;

	private Label label5;

	private ComboBox cbEndYear;

	private Label label3;

	private ComboBox cbStartYear;

	private Label label7;

	private NumericUpDown numWeight;

	private CheckBox chkManualTransmission;

	private TextBox txtFinalGearRatio;

	private Label label14;

	private TextBox txtGearRatio6;

	private Label label12;

	private TextBox txtGearRatio5;

	private Label label13;

	private TextBox txtGearRatio4;

	private Label label10;

	private TextBox txtGearRatio3;

	private Label label11;

	private TextBox txtGearRatio2;

	private Label label9;

	private TextBox txtGearRatio1;

	private Label label8;

	private GroupBox groupBox1;

	private Label label15;

	private NumericUpDown numDragCoefficient;

	private Label label16;

	private NumericUpDown numFrontalArea;

	private Label lengthType;

	private Label label19;

	private NumericUpDown numTireDiameter;

	private Label areaType;

	private GroupBox groupBox3;

	private GroupBox groupBox2;

	private ContextMenuStrip rightClickMenu;

	private ToolStripMenuItem addCarDefinitionToolStripMenuItem;

	private Label weightType;

	private Button btnSave;

	private StatusStrip statusStrip1;

	private ToolStripStatusLabel txtStatus;

	private ErrorProvider errorProvider;

	public CarEditor()
	{
		InitializeComponent();
		clearFields();
		PopulateCarDropdown();
		PopulateYearDropdowns();
	}

	private void PopulateCarDropdown()
	{
		tvCarList.Nodes.Clear();
		tvCarList.Nodes.AddRange(Statics.PopulateCarDropdown());
		txtStatus.Text = "Loaded cars";
	}

	private void PopulateYearDropdowns()
	{
		int num = DateTime.Now.Year;
		cbStartYear.BeginUpdate();
		cbEndYear.BeginUpdate();
		while (num >= 1980)
		{
			cbStartYear.Items.Add(num);
			cbEndYear.Items.Add(num);
			num--;
		}
		cbStartYear.EndUpdate();
		cbEndYear.EndUpdate();
	}

	private void clearFields()
	{
		cbStartYear.SelectedText = string.Empty;
		cbEndYear.SelectedText = string.Empty;
		cbStartYear.SelectedIndex = -1;
		cbEndYear.SelectedIndex = -1;
		txtMake.Text = string.Empty;
		txtModel.Text = string.Empty;
		txtSubModel.Text = string.Empty;
		numWeight.Text = string.Empty;
		numDragCoefficient.Text = string.Empty;
		numFrontalArea.Text = string.Empty;
		numTireDiameter.Text = string.Empty;
		txtGearRatio1.Text = string.Empty;
		txtGearRatio2.Text = string.Empty;
		txtGearRatio3.Text = string.Empty;
		txtGearRatio4.Text = string.Empty;
		txtGearRatio5.Text = string.Empty;
		txtGearRatio6.Text = string.Empty;
		txtFinalGearRatio.Text = string.Empty;
	}

	private bool hasInvalidInputFields()
	{
		bool result = false;
		errorProvider.Clear();
		if (!Regex.IsMatch(cbStartYear.Text.Trim(), "^\\d+$"))
		{
			errorProvider.SetError(cbStartYear, "Input for Start Year was invalid.");
			result = true;
		}
		if (!Regex.IsMatch(cbEndYear.Text.Trim(), "^\\d+$"))
		{
			errorProvider.SetError(cbEndYear, "Input for End Year was invalid.");
			result = true;
		}
		if (!Regex.IsMatch(numWeight.Text.Trim(), "^\\d+$"))
		{
			errorProvider.SetError(weightType, "Input for Weight was invalid.");
			result = true;
		}
		if (!double.TryParse(txtFinalGearRatio.Text.Trim(), out var result2))
		{
			errorProvider.SetError(txtFinalGearRatio, "Input for Final Drive Ratio was invalid.");
			result = true;
		}
		if (!double.TryParse(numDragCoefficient.Text.Trim(), out result2))
		{
			errorProvider.SetError(numDragCoefficient, "Input for Drag Coefficient was invalid.");
			result = true;
		}
		if (!double.TryParse(numFrontalArea.Text.Trim(), out result2))
		{
			errorProvider.SetError(areaType, "Input for Frontal Area was invalid.");
			result = true;
		}
		if (!double.TryParse(numTireDiameter.Text.Trim(), out result2))
		{
			errorProvider.SetError(lengthType, "Input for Tire Diameter was invalid.");
			result = true;
		}
		return result;
	}

	private void btnSave_Click(object sender, EventArgs e)
	{
		if (hasInvalidInputFields())
		{
			return;
		}
		using (SaveFileDialog saveFileDialog = new SaveFileDialog())
		{
			saveFileDialog.Filter = General.FileDialogFilter_SaveCustomCar;
			saveFileDialog.InitialDirectory = Path.Combine(Statics.baseFilepath, "CustomCars");
			saveFileDialog.Title = General.CarEditor_Save_DialogTitle;
			saveFileDialog.RestoreDirectory = true;
			saveFileDialog.AutoUpgradeEnabled = true;
			saveFileDialog.OverwritePrompt = false;
			if (saveFileDialog.ShowDialog() == DialogResult.OK)
			{
				string fileName = saveFileDialog.FileName;
				CarDataset carDataset = new CarDataset();
				CarDataset.CarParametersRow carParametersRow = carDataset.CarParameters.AddCarParametersRow(txtMake.Text.Trim(), txtModel.Text.Trim(), txtSubModel.Text.Trim(), Convert.ToInt32(cbStartYear.Text.Trim()), Convert.ToInt32(cbEndYear.Text.Trim()), Convert.ToInt32(numWeight.Value), chkManualTransmission.Checked, string.IsNullOrEmpty(txtGearRatio1.Text.Trim()) ? 0.0 : Convert.ToDouble(txtGearRatio1.Text.Trim()), string.IsNullOrEmpty(txtGearRatio2.Text.Trim()) ? 0.0 : Convert.ToDouble(txtGearRatio2.Text.Trim()), string.IsNullOrEmpty(txtGearRatio3.Text.Trim()) ? 0.0 : Convert.ToDouble(txtGearRatio3.Text.Trim()), string.IsNullOrEmpty(txtGearRatio4.Text.Trim()) ? 0.0 : Convert.ToDouble(txtGearRatio4.Text.Trim()), string.IsNullOrEmpty(txtGearRatio5.Text.Trim()) ? 0.0 : Convert.ToDouble(txtGearRatio5.Text.Trim()), string.IsNullOrEmpty(txtGearRatio6.Text.Trim()) ? 0.0 : Convert.ToDouble(txtGearRatio6.Text.Trim()), Convert.ToDouble(txtFinalGearRatio.Text.Trim()), Convert.ToDouble(numDragCoefficient.Value), Convert.ToDouble(numFrontalArea.Value), Convert.ToDouble(numTireDiameter.Value), Custom: true);
				if (string.IsNullOrEmpty(txtGearRatio1.Text.Trim()))
				{
					carParametersRow.SetTransGear1Null();
				}
				if (string.IsNullOrEmpty(txtGearRatio2.Text.Trim()))
				{
					carParametersRow.SetTransGear2Null();
				}
				if (string.IsNullOrEmpty(txtGearRatio3.Text.Trim()))
				{
					carParametersRow.SetTransGear3Null();
				}
				if (string.IsNullOrEmpty(txtGearRatio4.Text.Trim()))
				{
					carParametersRow.SetTransGear4Null();
				}
				if (string.IsNullOrEmpty(txtGearRatio5.Text.Trim()))
				{
					carParametersRow.SetTransGear5Null();
				}
				if (string.IsNullOrEmpty(txtGearRatio6.Text.Trim()))
				{
					carParametersRow.SetTransGear6Null();
				}
				bool flag = true;
				if (File.Exists(fileName))
				{
					flag = false;
					if (DialogResult.Yes == MessageBox.Show("Car definition file already exists.  Do you want to overwrite the existing data file?", "File Exists", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2))
					{
						flag = true;
					}
				}
				if (!saveFileDialog.FileName.Contains(Path.Combine(Statics.baseFilepath, "CustomCars")))
				{
					flag = false;
					MessageBox.Show("File not saved.  File outside of Custom base folder.", "Invalid save location", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button1);
				}
				if (flag)
				{
					carDataset.WriteXml(fileName);
					PopupMessage popupMessage = new PopupMessage("Saved", 1);
					popupMessage.Location = new Point(base.Width / 2 - popupMessage.Width / 2 + base.Location.X, base.Height / 2 - popupMessage.Height / 2 + base.Location.Y);
					popupMessage.Show(this);
				}
			}
		}
		PopulateCarDropdown();
	}

	private void tvCarList_AfterSelect(object sender, TreeViewEventArgs e)
	{
		if (e.Node.Nodes.Count != 0)
		{
			return;
		}
		clearFields();
		DataRow dataRow = (DataRow)e.Node.Tag;
		if (dataRow == null)
		{
			return;
		}
		try
		{
			txtMake.Text = dataRow["CarMake"].ToString().Trim();
			txtModel.Text = dataRow["CarModel"].ToString().Trim();
			txtSubModel.Text = dataRow["CarSubModel"].ToString().Trim();
			numWeight.Text = dataRow["Weight"].ToString().Trim();
			numDragCoefficient.Text = dataRow["DragCoefficient"].ToString().Trim();
			numFrontalArea.Text = dataRow["FrontalArea"].ToString().Trim();
			numTireDiameter.Text = dataRow["TireDiameter"].ToString().Trim();
			if (double.TryParse(dataRow["TransGear1"].ToString(), out var result))
			{
				txtGearRatio1.Text = double.Parse(dataRow["TransGear1"].ToString()).ToString("F3").Trim();
			}
			if (double.TryParse(dataRow["TransGear2"].ToString(), out result))
			{
				txtGearRatio2.Text = double.Parse(dataRow["TransGear2"].ToString()).ToString("F3").Trim();
			}
			if (double.TryParse(dataRow["TransGear3"].ToString(), out result))
			{
				txtGearRatio3.Text = double.Parse(dataRow["TransGear3"].ToString()).ToString("F3").Trim();
			}
			if (double.TryParse(dataRow["TransGear4"].ToString(), out result))
			{
				txtGearRatio4.Text = double.Parse(dataRow["TransGear4"].ToString()).ToString("F3").Trim();
			}
			if (double.TryParse(dataRow["TransGear5"].ToString(), out result))
			{
				txtGearRatio5.Text = double.Parse(dataRow["TransGear5"].ToString()).ToString("F3").Trim();
			}
			if (double.TryParse(dataRow["TransGear6"].ToString(), out result))
			{
				txtGearRatio6.Text = double.Parse(dataRow["TransGear6"].ToString()).ToString("F3").Trim();
			}
			if (double.TryParse(dataRow["FinalGearRatio"].ToString(), out result))
			{
				txtFinalGearRatio.Text = double.Parse(dataRow["FinalGearRatio"].ToString()).ToString("F3").Trim();
			}
			chkManualTransmission.Checked = Convert.ToBoolean(dataRow["TransTypeManual"].ToString().Trim());
			cbStartYear.SelectedIndex = cbStartYear.FindString(dataRow["StartYear"].ToString().Trim().Trim());
			cbEndYear.SelectedIndex = cbEndYear.FindString(dataRow["EndYear"].ToString().Trim().Trim());
			txtStatus.Text = "Viewing " + txtMake.Text + " " + txtModel.Text + " " + txtSubModel.Text;
		}
		catch (Exception ex)
		{
			throw new Exception("E403: " + ex);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VirtualDyno.CarEditor));
		this.tvCarList = new System.Windows.Forms.TreeView();
		this.splitContainer1 = new System.Windows.Forms.SplitContainer();
		this.statusStrip1 = new System.Windows.Forms.StatusStrip();
		this.txtStatus = new System.Windows.Forms.ToolStripStatusLabel();
		this.btnSave = new System.Windows.Forms.Button();
		this.groupBox3 = new System.Windows.Forms.GroupBox();
		this.weightType = new System.Windows.Forms.Label();
		this.numWeight = new System.Windows.Forms.NumericUpDown();
		this.label7 = new System.Windows.Forms.Label();
		this.lengthType = new System.Windows.Forms.Label();
		this.numDragCoefficient = new System.Windows.Forms.NumericUpDown();
		this.label19 = new System.Windows.Forms.Label();
		this.label15 = new System.Windows.Forms.Label();
		this.numTireDiameter = new System.Windows.Forms.NumericUpDown();
		this.numFrontalArea = new System.Windows.Forms.NumericUpDown();
		this.areaType = new System.Windows.Forms.Label();
		this.label16 = new System.Windows.Forms.Label();
		this.groupBox2 = new System.Windows.Forms.GroupBox();
		this.cbEndYear = new System.Windows.Forms.ComboBox();
		this.cbStartYear = new System.Windows.Forms.ComboBox();
		this.label3 = new System.Windows.Forms.Label();
		this.label5 = new System.Windows.Forms.Label();
		this.txtSubModel = new System.Windows.Forms.TextBox();
		this.label1 = new System.Windows.Forms.Label();
		this.txtMake = new System.Windows.Forms.TextBox();
		this.label2 = new System.Windows.Forms.Label();
		this.txtModel = new System.Windows.Forms.TextBox();
		this.label4 = new System.Windows.Forms.Label();
		this.groupBox1 = new System.Windows.Forms.GroupBox();
		this.label14 = new System.Windows.Forms.Label();
		this.txtGearRatio6 = new System.Windows.Forms.TextBox();
		this.txtFinalGearRatio = new System.Windows.Forms.TextBox();
		this.label12 = new System.Windows.Forms.Label();
		this.txtGearRatio1 = new System.Windows.Forms.TextBox();
		this.txtGearRatio5 = new System.Windows.Forms.TextBox();
		this.label8 = new System.Windows.Forms.Label();
		this.label13 = new System.Windows.Forms.Label();
		this.label9 = new System.Windows.Forms.Label();
		this.txtGearRatio4 = new System.Windows.Forms.TextBox();
		this.chkManualTransmission = new System.Windows.Forms.CheckBox();
		this.txtGearRatio2 = new System.Windows.Forms.TextBox();
		this.label10 = new System.Windows.Forms.Label();
		this.label11 = new System.Windows.Forms.Label();
		this.txtGearRatio3 = new System.Windows.Forms.TextBox();
		this.rightClickMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
		this.addCarDefinitionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
		this.splitContainer1.Panel1.SuspendLayout();
		this.splitContainer1.Panel2.SuspendLayout();
		this.splitContainer1.SuspendLayout();
		this.statusStrip1.SuspendLayout();
		this.groupBox3.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.numWeight).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numDragCoefficient).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numTireDiameter).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numFrontalArea).BeginInit();
		this.groupBox2.SuspendLayout();
		this.groupBox1.SuspendLayout();
		this.rightClickMenu.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.errorProvider).BeginInit();
		base.SuspendLayout();
		this.tvCarList.Dock = System.Windows.Forms.DockStyle.Fill;
		this.tvCarList.Location = new System.Drawing.Point(0, 0);
		this.tvCarList.Name = "tvCarList";
		this.tvCarList.Size = new System.Drawing.Size(231, 330);
		this.tvCarList.TabIndex = 0;
		this.tvCarList.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(tvCarList_AfterSelect);
		this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.splitContainer1.Location = new System.Drawing.Point(0, 0);
		this.splitContainer1.Name = "splitContainer1";
		this.splitContainer1.Panel1.Controls.Add(this.tvCarList);
		this.splitContainer1.Panel2.Controls.Add(this.statusStrip1);
		this.splitContainer1.Panel2.Controls.Add(this.btnSave);
		this.splitContainer1.Panel2.Controls.Add(this.groupBox3);
		this.splitContainer1.Panel2.Controls.Add(this.groupBox2);
		this.splitContainer1.Panel2.Controls.Add(this.groupBox1);
		this.splitContainer1.Size = new System.Drawing.Size(626, 330);
		this.splitContainer1.SplitterDistance = 231;
		this.splitContainer1.TabIndex = 1;
		this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.txtStatus });
		this.statusStrip1.Location = new System.Drawing.Point(0, 308);
		this.statusStrip1.Name = "statusStrip1";
		this.statusStrip1.Size = new System.Drawing.Size(391, 22);
		this.statusStrip1.TabIndex = 40;
		this.statusStrip1.Text = "statusStrip1";
		this.txtStatus.Name = "txtStatus";
		this.txtStatus.Size = new System.Drawing.Size(0, 17);
		this.btnSave.Image = VirtualDyno.Properties.Resources.Car16x16;
		this.btnSave.Location = new System.Drawing.Point(226, 252);
		this.btnSave.Name = "btnSave";
		this.btnSave.Size = new System.Drawing.Size(153, 50);
		this.btnSave.TabIndex = 39;
		this.btnSave.Text = "&Save as Custom";
		this.btnSave.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.btnSave.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		this.btnSave.UseVisualStyleBackColor = true;
		this.btnSave.Click += new System.EventHandler(btnSave_Click);
		this.groupBox3.Controls.Add(this.weightType);
		this.groupBox3.Controls.Add(this.numWeight);
		this.groupBox3.Controls.Add(this.label7);
		this.groupBox3.Controls.Add(this.lengthType);
		this.groupBox3.Controls.Add(this.numDragCoefficient);
		this.groupBox3.Controls.Add(this.label19);
		this.groupBox3.Controls.Add(this.label15);
		this.groupBox3.Controls.Add(this.numTireDiameter);
		this.groupBox3.Controls.Add(this.numFrontalArea);
		this.groupBox3.Controls.Add(this.areaType);
		this.groupBox3.Controls.Add(this.label16);
		this.groupBox3.Location = new System.Drawing.Point(10, 179);
		this.groupBox3.Name = "groupBox3";
		this.groupBox3.Size = new System.Drawing.Size(210, 123);
		this.groupBox3.TabIndex = 38;
		this.groupBox3.TabStop = false;
		this.groupBox3.Text = "Properties";
		this.weightType.AutoSize = true;
		this.weightType.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.weightType.Location = new System.Drawing.Point(153, 22);
		this.weightType.Name = "weightType";
		this.weightType.Size = new System.Drawing.Size(23, 15);
		this.weightType.TabIndex = 37;
		this.weightType.Text = "lbs";
		this.numWeight.Location = new System.Drawing.Point(90, 19);
		this.numWeight.Maximum = new decimal(new int[4] { 9999, 0, 0, 0 });
		this.numWeight.Minimum = new decimal(new int[4] { 1500, 0, 0, 0 });
		this.numWeight.Name = "numWeight";
		this.numWeight.Size = new System.Drawing.Size(62, 20);
		this.numWeight.TabIndex = 11;
		this.numWeight.Value = new decimal(new int[4] { 1500, 0, 0, 0 });
		this.label7.AutoSize = true;
		this.label7.Location = new System.Drawing.Point(43, 22);
		this.label7.Name = "label7";
		this.label7.Size = new System.Drawing.Size(41, 13);
		this.label7.TabIndex = 12;
		this.label7.Text = "Weight";
		this.lengthType.AutoSize = true;
		this.lengthType.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lengthType.Location = new System.Drawing.Point(146, 98);
		this.lengthType.Name = "lengthType";
		this.lengthType.Size = new System.Drawing.Size(43, 15);
		this.lengthType.TabIndex = 36;
		this.lengthType.Text = "inches";
		this.numDragCoefficient.DecimalPlaces = 2;
		this.numDragCoefficient.Increment = new decimal(new int[4] { 1, 0, 0, 131072 });
		this.numDragCoefficient.Location = new System.Drawing.Point(90, 45);
		this.numDragCoefficient.Maximum = new decimal(new int[4] { 99, 0, 0, 131072 });
		this.numDragCoefficient.Minimum = new decimal(new int[4] { 10, 0, 0, 131072 });
		this.numDragCoefficient.Name = "numDragCoefficient";
		this.numDragCoefficient.Size = new System.Drawing.Size(44, 20);
		this.numDragCoefficient.TabIndex = 29;
		this.numDragCoefficient.Value = new decimal(new int[4] { 35, 0, 0, 131072 });
		this.label19.AutoSize = true;
		this.label19.Location = new System.Drawing.Point(17, 98);
		this.label19.Name = "label19";
		this.label19.Size = new System.Drawing.Size(70, 13);
		this.label19.TabIndex = 35;
		this.label19.Text = "Tire Diameter";
		this.label15.AutoSize = true;
		this.label15.Location = new System.Drawing.Point(5, 47);
		this.label15.Name = "label15";
		this.label15.Size = new System.Drawing.Size(83, 13);
		this.label15.TabIndex = 30;
		this.label15.Text = "Drag Coefficient";
		this.numTireDiameter.DecimalPlaces = 2;
		this.numTireDiameter.Increment = new decimal(new int[4] { 1, 0, 0, 131072 });
		this.numTireDiameter.Location = new System.Drawing.Point(90, 96);
		this.numTireDiameter.Maximum = new decimal(new int[4] { 9999, 0, 0, 131072 });
		this.numTireDiameter.Minimum = new decimal(new int[4] { 1000, 0, 0, 131072 });
		this.numTireDiameter.Name = "numTireDiameter";
		this.numTireDiameter.Size = new System.Drawing.Size(54, 20);
		this.numTireDiameter.TabIndex = 34;
		this.numTireDiameter.Value = new decimal(new int[4] { 2500, 0, 0, 131072 });
		this.numFrontalArea.DecimalPlaces = 2;
		this.numFrontalArea.Increment = new decimal(new int[4] { 1, 0, 0, 131072 });
		this.numFrontalArea.Location = new System.Drawing.Point(90, 70);
		this.numFrontalArea.Maximum = new decimal(new int[4] { 9999, 0, 0, 131072 });
		this.numFrontalArea.Minimum = new decimal(new int[4] { 1000, 0, 0, 131072 });
		this.numFrontalArea.Name = "numFrontalArea";
		this.numFrontalArea.Size = new System.Drawing.Size(54, 20);
		this.numFrontalArea.TabIndex = 31;
		this.numFrontalArea.Value = new decimal(new int[4] { 2500, 0, 0, 131072 });
		this.areaType.AutoSize = true;
		this.areaType.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.areaType.Location = new System.Drawing.Point(146, 73);
		this.areaType.Name = "areaType";
		this.areaType.Size = new System.Drawing.Size(17, 15);
		this.areaType.TabIndex = 33;
		this.areaType.Text = "ft²";
		this.label16.AutoSize = true;
		this.label16.Location = new System.Drawing.Point(23, 72);
		this.label16.Name = "label16";
		this.label16.Size = new System.Drawing.Size(64, 13);
		this.label16.TabIndex = 32;
		this.label16.Text = "Frontal Area";
		this.groupBox2.Controls.Add(this.cbEndYear);
		this.groupBox2.Controls.Add(this.cbStartYear);
		this.groupBox2.Controls.Add(this.label3);
		this.groupBox2.Controls.Add(this.label5);
		this.groupBox2.Controls.Add(this.txtSubModel);
		this.groupBox2.Controls.Add(this.label1);
		this.groupBox2.Controls.Add(this.txtMake);
		this.groupBox2.Controls.Add(this.label2);
		this.groupBox2.Controls.Add(this.txtModel);
		this.groupBox2.Controls.Add(this.label4);
		this.groupBox2.Location = new System.Drawing.Point(10, 9);
		this.groupBox2.Name = "groupBox2";
		this.groupBox2.Size = new System.Drawing.Size(210, 155);
		this.groupBox2.TabIndex = 37;
		this.groupBox2.TabStop = false;
		this.groupBox2.Text = "Description";
		this.cbEndYear.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cbEndYear.FormattingEnabled = true;
		this.cbEndYear.Location = new System.Drawing.Point(69, 48);
		this.cbEndYear.Name = "cbEndYear";
		this.cbEndYear.Size = new System.Drawing.Size(51, 21);
		this.cbEndYear.TabIndex = 8;
		this.cbStartYear.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cbStartYear.FormattingEnabled = true;
		this.cbStartYear.Location = new System.Drawing.Point(69, 19);
		this.cbStartYear.Name = "cbStartYear";
		this.cbStartYear.Size = new System.Drawing.Size(51, 21);
		this.cbStartYear.TabIndex = 6;
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(9, 22);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(54, 13);
		this.label3.TabIndex = 7;
		this.label3.Text = "Start Year";
		this.label5.AutoSize = true;
		this.label5.Location = new System.Drawing.Point(12, 51);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(51, 13);
		this.label5.TabIndex = 9;
		this.label5.Text = "End Year";
		this.txtSubModel.Location = new System.Drawing.Point(69, 126);
		this.txtSubModel.Name = "txtSubModel";
		this.txtSubModel.Size = new System.Drawing.Size(127, 20);
		this.txtSubModel.TabIndex = 5;
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(29, 77);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(34, 13);
		this.label1.TabIndex = 0;
		this.label1.Text = "Make";
		this.txtMake.Location = new System.Drawing.Point(69, 74);
		this.txtMake.Name = "txtMake";
		this.txtMake.Size = new System.Drawing.Size(127, 20);
		this.txtMake.TabIndex = 1;
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(29, 103);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(36, 13);
		this.label2.TabIndex = 2;
		this.label2.Text = "Model";
		this.txtModel.Location = new System.Drawing.Point(69, 100);
		this.txtModel.Name = "txtModel";
		this.txtModel.Size = new System.Drawing.Size(127, 20);
		this.txtModel.TabIndex = 3;
		this.label4.AutoSize = true;
		this.label4.Location = new System.Drawing.Point(9, 129);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(54, 13);
		this.label4.TabIndex = 4;
		this.label4.Text = "Submodel";
		this.groupBox1.Controls.Add(this.label14);
		this.groupBox1.Controls.Add(this.txtGearRatio6);
		this.groupBox1.Controls.Add(this.txtFinalGearRatio);
		this.groupBox1.Controls.Add(this.label12);
		this.groupBox1.Controls.Add(this.txtGearRatio1);
		this.groupBox1.Controls.Add(this.txtGearRatio5);
		this.groupBox1.Controls.Add(this.label8);
		this.groupBox1.Controls.Add(this.label13);
		this.groupBox1.Controls.Add(this.label9);
		this.groupBox1.Controls.Add(this.txtGearRatio4);
		this.groupBox1.Controls.Add(this.chkManualTransmission);
		this.groupBox1.Controls.Add(this.txtGearRatio2);
		this.groupBox1.Controls.Add(this.label10);
		this.groupBox1.Controls.Add(this.label11);
		this.groupBox1.Controls.Add(this.txtGearRatio3);
		this.groupBox1.Location = new System.Drawing.Point(226, 9);
		this.groupBox1.Name = "groupBox1";
		this.groupBox1.Size = new System.Drawing.Size(153, 227);
		this.groupBox1.TabIndex = 28;
		this.groupBox1.TabStop = false;
		this.groupBox1.Text = "Transmission";
		this.label14.AutoSize = true;
		this.label14.Location = new System.Drawing.Point(2, 203);
		this.label14.Name = "label14";
		this.label14.Size = new System.Drawing.Size(55, 13);
		this.label14.TabIndex = 26;
		this.label14.Text = "Final Gear";
		this.txtGearRatio6.Location = new System.Drawing.Point(63, 166);
		this.txtGearRatio6.Name = "txtGearRatio6";
		this.txtGearRatio6.Size = new System.Drawing.Size(56, 20);
		this.txtGearRatio6.TabIndex = 25;
		this.txtFinalGearRatio.Location = new System.Drawing.Point(63, 200);
		this.txtFinalGearRatio.Name = "txtFinalGearRatio";
		this.txtFinalGearRatio.Size = new System.Drawing.Size(56, 20);
		this.txtFinalGearRatio.TabIndex = 27;
		this.label12.AutoSize = true;
		this.label12.Location = new System.Drawing.Point(18, 170);
		this.label12.Name = "label12";
		this.label12.Size = new System.Drawing.Size(39, 13);
		this.label12.TabIndex = 24;
		this.label12.Text = "Gear 6";
		this.txtGearRatio1.Location = new System.Drawing.Point(63, 45);
		this.txtGearRatio1.Name = "txtGearRatio1";
		this.txtGearRatio1.Size = new System.Drawing.Size(56, 20);
		this.txtGearRatio1.TabIndex = 15;
		this.txtGearRatio5.Location = new System.Drawing.Point(63, 142);
		this.txtGearRatio5.Name = "txtGearRatio5";
		this.txtGearRatio5.Size = new System.Drawing.Size(56, 20);
		this.txtGearRatio5.TabIndex = 23;
		this.label8.AutoSize = true;
		this.label8.Location = new System.Drawing.Point(18, 49);
		this.label8.Name = "label8";
		this.label8.Size = new System.Drawing.Size(39, 13);
		this.label8.TabIndex = 14;
		this.label8.Text = "Gear 1";
		this.label13.AutoSize = true;
		this.label13.Location = new System.Drawing.Point(18, 146);
		this.label13.Name = "label13";
		this.label13.Size = new System.Drawing.Size(39, 13);
		this.label13.TabIndex = 22;
		this.label13.Text = "Gear 5";
		this.label9.AutoSize = true;
		this.label9.Location = new System.Drawing.Point(18, 73);
		this.label9.Name = "label9";
		this.label9.Size = new System.Drawing.Size(39, 13);
		this.label9.TabIndex = 16;
		this.label9.Text = "Gear 2";
		this.txtGearRatio4.Location = new System.Drawing.Point(63, 119);
		this.txtGearRatio4.Name = "txtGearRatio4";
		this.txtGearRatio4.Size = new System.Drawing.Size(56, 20);
		this.txtGearRatio4.TabIndex = 21;
		this.chkManualTransmission.AutoSize = true;
		this.chkManualTransmission.Location = new System.Drawing.Point(63, 21);
		this.chkManualTransmission.Name = "chkManualTransmission";
		this.chkManualTransmission.Size = new System.Drawing.Size(61, 17);
		this.chkManualTransmission.TabIndex = 13;
		this.chkManualTransmission.Text = "Manual";
		this.chkManualTransmission.UseVisualStyleBackColor = true;
		this.txtGearRatio2.Location = new System.Drawing.Point(63, 69);
		this.txtGearRatio2.Name = "txtGearRatio2";
		this.txtGearRatio2.Size = new System.Drawing.Size(56, 20);
		this.txtGearRatio2.TabIndex = 17;
		this.label10.AutoSize = true;
		this.label10.Location = new System.Drawing.Point(18, 123);
		this.label10.Name = "label10";
		this.label10.Size = new System.Drawing.Size(39, 13);
		this.label10.TabIndex = 20;
		this.label10.Text = "Gear 4";
		this.label11.AutoSize = true;
		this.label11.Location = new System.Drawing.Point(18, 99);
		this.label11.Name = "label11";
		this.label11.Size = new System.Drawing.Size(39, 13);
		this.label11.TabIndex = 18;
		this.label11.Text = "Gear 3";
		this.txtGearRatio3.Location = new System.Drawing.Point(63, 95);
		this.txtGearRatio3.Name = "txtGearRatio3";
		this.txtGearRatio3.Size = new System.Drawing.Size(56, 20);
		this.txtGearRatio3.TabIndex = 19;
		this.rightClickMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.addCarDefinitionToolStripMenuItem });
		this.rightClickMenu.Name = "contextMenuStrip1";
		this.rightClickMenu.Size = new System.Drawing.Size(173, 26);
		this.addCarDefinitionToolStripMenuItem.Image = VirtualDyno.Properties.Resources.Car16x16;
		this.addCarDefinitionToolStripMenuItem.Name = "addCarDefinitionToolStripMenuItem";
		this.addCarDefinitionToolStripMenuItem.Size = new System.Drawing.Size(172, 22);
		this.addCarDefinitionToolStripMenuItem.Text = "Add Car Definition";
		this.errorProvider.ContainerControl = this;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(626, 330);
		base.Controls.Add(this.splitContainer1);
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.Name = "CarEditor";
		this.Text = "CarEditor";
		this.splitContainer1.Panel1.ResumeLayout(false);
		this.splitContainer1.Panel2.ResumeLayout(false);
		this.splitContainer1.Panel2.PerformLayout();
		this.splitContainer1.ResumeLayout(false);
		this.statusStrip1.ResumeLayout(false);
		this.statusStrip1.PerformLayout();
		this.groupBox3.ResumeLayout(false);
		this.groupBox3.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.numWeight).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numDragCoefficient).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numTireDiameter).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numFrontalArea).EndInit();
		this.groupBox2.ResumeLayout(false);
		this.groupBox2.PerformLayout();
		this.groupBox1.ResumeLayout(false);
		this.groupBox1.PerformLayout();
		this.rightClickMenu.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.errorProvider).EndInit();
		base.ResumeLayout(false);
	}
}
