using System.Net.NetworkInformation;
using System.IO.Pipes;
using System.Text.Json;
using IpChanger.Common;

namespace IpChanger.UI;

public partial class MainForm : Form
{
    private ComboBox cmbAdapters;
    private CheckBox chkDhcp;
    private TextBox txtIp;
    private TextBox txtSubnet;
    private TextBox txtGateway;
    private TextBox txtDns;
    private Button btnApply;
    private Label lblStatus;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblServiceStatus;
    private System.Windows.Forms.Timer timerStatus;

    public MainForm()
    {
        InitializeComponent();
        LoadAdapters();
        
        // Start connection check timer
        timerStatus = new System.Windows.Forms.Timer();
        timerStatus.Interval = 3000; // 3 seconds
        timerStatus.Tick += async (s, e) => await CheckConnectionStatus();
        timerStatus.Start();
        
        // Initial check
        _ = CheckConnectionStatus();
    }

    private async Task CheckConnectionStatus()
    {
        try
        {
            // Try to connect to the service without sending data
            await using var client = new NamedPipeClientStream(".", "IpChangerPipe", PipeDirection.InOut);
            await client.ConnectAsync(500); // Short timeout
            lblServiceStatus.Text = "Service: Connected";
            lblServiceStatus.ForeColor = Color.Green;
        }
        catch
        {
            lblServiceStatus.Text = "Service: Disconnected";
            lblServiceStatus.ForeColor = Color.Red;
        }
    }

    private void LoadAdapters()
    {
        cmbAdapters.Items.Clear();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                cmbAdapters.Items.Add(new AdapterItem(nic));
            }
        }
        if (cmbAdapters.Items.Count > 0) cmbAdapters.SelectedIndex = 0;
    }

    private void btnApply_Click(object sender, EventArgs e)
    {
        if (cmbAdapters.SelectedItem is not AdapterItem selected) return;

        var request = new IpConfigRequest
        {
            AdapterId = selected.Id,
            UseDhcp = chkDhcp.Checked,
            IpAddress = txtIp.Text,
            SubnetMask = txtSubnet.Text,
            Gateway = txtGateway.Text,
            Dns = txtDns.Text
        };

        ApplySettings(request);
    }

    private async void ApplySettings(IpConfigRequest request)
    {
        btnApply.Enabled = false;
        lblStatus.Text = "Connecting to service...";
        try
        {
            await using var client = new NamedPipeClientStream(".", "IpChangerPipe", PipeDirection.InOut);
            await client.ConnectAsync(3000);

            // Use leaveOpen: true to prevent StreamWriter/StreamReader from closing the pipe prematurely
            await using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);

            await writer.WriteLineAsync(JsonSerializer.Serialize(request));
            
            var responseJson = await reader.ReadLineAsync();
            if (responseJson != null)
            {
                var response = JsonSerializer.Deserialize<IpConfigResponse>(responseJson);
                lblStatus.Text = response?.Message ?? "Unknown response";
                MessageBox.Show(response?.Message, response?.Success == true ? "Success" : "Error");
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Error: " + ex.Message;
            MessageBox.Show("Could not connect to service. Is it installed and running?\n" + ex.Message, "Connection Error");
        }
        finally
        {
            btnApply.Enabled = true;
        }
    }

    private void chkDhcp_CheckedChanged(object sender, EventArgs e)
    {
        txtIp.Enabled = !chkDhcp.Checked;
        txtSubnet.Enabled = !chkDhcp.Checked;
        txtGateway.Enabled = !chkDhcp.Checked;
        txtDns.Enabled = !chkDhcp.Checked;
    }

    private class AdapterItem
    {
        public string Name { get; }
        public string Id { get; }
        public AdapterItem(NetworkInterface nic)
        {
            Name = $"{nic.Name} ({nic.Description})";
            Id = nic.Id;
        }
        public override string ToString() => Name;
    }

    private void InitializeComponent()
    {
        this.Text = "IP Changer Tool";
        this.Size = new Size(400, 400);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        int y = 20;
        int lblW = 100;
        int txtW = 200;
        int h = 25;

        var lblAdapter = new Label { Text = "Adapter:", Location = new Point(20, y), Size = new Size(lblW, h) };
        cmbAdapters = new ComboBox { Location = new Point(120, y), Size = new Size(240, h), DropDownStyle = ComboBoxStyle.DropDownList };
        y += 40;

        chkDhcp = new CheckBox { Text = "Obtain IP Automatically (DHCP)", Location = new Point(120, y), Size = new Size(240, h) };
        chkDhcp.CheckedChanged += chkDhcp_CheckedChanged;
        y += 40;

        var lblIp = new Label { Text = "IP Address:", Location = new Point(20, y), Size = new Size(lblW, h) };
        txtIp = new TextBox { Location = new Point(120, y), Size = new Size(txtW, h) };
        y += 35;

        var lblSubnet = new Label { Text = "Subnet Mask:", Location = new Point(20, y), Size = new Size(lblW, h) };
        txtSubnet = new TextBox { Text = "255.255.255.0", Location = new Point(120, y), Size = new Size(txtW, h) };
        y += 35;

        var lblGateway = new Label { Text = "Gateway:", Location = new Point(20, y), Size = new Size(lblW, h) };
        txtGateway = new TextBox { Location = new Point(120, y), Size = new Size(txtW, h) };
        y += 35;

        var lblDns = new Label { Text = "DNS:", Location = new Point(20, y), Size = new Size(lblW, h) };
        txtDns = new TextBox { Text = "8.8.8.8", Location = new Point(120, y), Size = new Size(txtW, h) };
        y += 45;

        btnApply = new Button { Text = "Apply Settings", Location = new Point(120, y), Size = new Size(120, 40) };
        btnApply.Click += btnApply_Click;
        y += 50;

        lblStatus = new Label { Text = "Ready", Location = new Point(20, y), Size = new Size(340, h), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };

        statusStrip = new StatusStrip();
        lblServiceStatus = new ToolStripStatusLabel { Text = "Checking Service..." };
        statusStrip.Items.Add(lblServiceStatus);

        this.Controls.AddRange(new Control[] { lblAdapter, cmbAdapters, chkDhcp, lblIp, txtIp, lblSubnet, txtSubnet, lblGateway, txtGateway, lblDns, txtDns, btnApply, lblStatus, statusStrip });
    }
}
