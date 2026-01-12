using System.Net.NetworkInformation;
using System.IO.Pipes;
using System.Reflection;
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
    private Button btnRefresh;
    private Button btnCopy;
    private Label lblStatus;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblServiceStatus;
    private ToolStripStatusLabel lblVersion;
    private ToolTip toolTip;
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

    private bool ValidateInputs(out string errorMessage)
    {
        errorMessage = "";

        if (!chkDhcp.Checked)
        {
            // Validate IP Address
            if (!System.Net.IPAddress.TryParse(txtIp.Text.Trim(), out _))
            {
                errorMessage = "Invalid IP address format.";
                return false;
            }

            // Validate Subnet Mask
            if (!System.Net.IPAddress.TryParse(txtSubnet.Text.Trim(), out _))
            {
                errorMessage = "Invalid subnet mask format.";
                return false;
            }

            // Validate Gateway (optional - only if provided)
            if (!string.IsNullOrWhiteSpace(txtGateway.Text) &&
                !System.Net.IPAddress.TryParse(txtGateway.Text.Trim(), out _))
            {
                errorMessage = "Invalid gateway address format.";
                return false;
            }

            // Validate DNS (optional - comma-separated IPs)
            if (!string.IsNullOrWhiteSpace(txtDns.Text))
            {
                var dnsEntries = txtDns.Text.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var dns in dnsEntries)
                {
                    if (!System.Net.IPAddress.TryParse(dns.Trim(), out _))
                    {
                        errorMessage = $"Invalid DNS server format: {dns.Trim()}";
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private void btnApply_Click(object sender, EventArgs e)
    {
        if (cmbAdapters.SelectedItem is not AdapterItem selected) return;

        if (!ValidateInputs(out string validationError))
        {
            MessageBox.Show(validationError, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

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

    private void cmbAdapters_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (cmbAdapters.SelectedItem is AdapterItem item)
            LoadAdapterConfig(item);
    }

    private void LoadAdapterConfig(AdapterItem item)
    {
        try
        {
            var nic = item.Nic;
            var ipProps = nic.GetIPProperties();

            // Get IPv4 address and subnet mask
            var ipv4Address = ipProps.UnicastAddresses
                .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ipv4Address != null)
            {
                txtIp.Text = ipv4Address.Address.ToString();
                txtSubnet.Text = ipv4Address.IPv4Mask?.ToString() ?? "255.255.255.0";
            }
            else
            {
                txtIp.Text = string.Empty;
                txtSubnet.Text = "255.255.255.0";
            }

            // Get gateway
            var gateway = ipProps.GatewayAddresses
                .FirstOrDefault(gw => gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            txtGateway.Text = gateway?.Address.ToString() ?? string.Empty;

            // Get DNS servers
            var dnsServers = ipProps.DnsAddresses
                .Where(dns => dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(dns => dns.ToString());
            txtDns.Text = string.Join(",", dnsServers);

            // Check DHCP status
            try
            {
                var ipv4Props = ipProps.GetIPv4Properties();
                chkDhcp.Checked = ipv4Props.IsDhcpEnabled;
            }
            catch
            {
                // Some adapters may not support IPv4 properties
                chkDhcp.Checked = false;
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error loading config: {ex.Message}";
        }
    }

    private void btnCopy_Click(object sender, EventArgs e)
    {
        var adapter = cmbAdapters.SelectedItem as AdapterItem;
        var config = $"Adapter: {adapter?.Name ?? "None"}\n" +
                     $"DHCP: {(chkDhcp.Checked ? "Enabled" : "Disabled")}\n" +
                     $"IP Address: {txtIp.Text}\n" +
                     $"Subnet Mask: {txtSubnet.Text}\n" +
                     $"Gateway: {txtGateway.Text}\n" +
                     $"DNS: {txtDns.Text}";

        Clipboard.SetText(config);
        lblStatus.Text = "Configuration copied to clipboard.";
    }

    private class AdapterItem
    {
        public string Name { get; }
        public string Id { get; }
        public NetworkInterface Nic { get; }
        public AdapterItem(NetworkInterface nic)
        {
            Nic = nic;
            Name = $"{nic.Name} ({nic.Description})";
            Id = nic.Id;
        }
        public override string ToString() => Name;
    }

    private void InitializeComponent()
    {
        this.Text = "IPCT - IP Change Tool";
        this.Size = new Size(400, 440);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        // Set form icon from embedded resource
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "IPCT.ico");
        if (File.Exists(iconPath))
        {
            this.Icon = new Icon(iconPath);
        }

        // Initialize tooltip
        toolTip = new ToolTip();

        int y = 20;
        int lblW = 100;
        int txtW = 200;
        int h = 25;

        var lblAdapter = new Label { Text = "Adapter:", Location = new Point(20, y), Size = new Size(lblW, h) };
        cmbAdapters = new ComboBox { Location = new Point(120, y), Size = new Size(205, h), DropDownStyle = ComboBoxStyle.DropDownList };
        cmbAdapters.SelectedIndexChanged += cmbAdapters_SelectedIndexChanged;

        // Refresh button next to adapter dropdown
        btnRefresh = new Button { Text = "\u21BB", Location = new Point(330, y - 1), Size = new Size(30, 24) };
        btnRefresh.Click += (s, e) => LoadAdapters();
        y += 40;

        chkDhcp = new CheckBox { Text = "Obtain IP Automatically (DHCP)", Location = new Point(120, y), Size = new Size(240, h) };
        chkDhcp.CheckedChanged += chkDhcp_CheckedChanged;
        y += 40;

        var lblIp = new Label { Text = "IP Address:", Location = new Point(20, y), Size = new Size(lblW, h) };
        txtIp = new TextBox { Location = new Point(120, y), Size = new Size(txtW, h) };
        y += 35;

        var lblSubnet = new Label { Text = "Subnet Mask:", Location = new Point(20, y), Size = new Size(lblW, h) };
        txtSubnet = new TextBox { Location = new Point(120, y), Size = new Size(txtW, h) };
        y += 35;

        var lblGateway = new Label { Text = "Gateway:", Location = new Point(20, y), Size = new Size(lblW, h) };
        txtGateway = new TextBox { Location = new Point(120, y), Size = new Size(txtW, h) };
        y += 35;

        var lblDns = new Label { Text = "DNS:", Location = new Point(20, y), Size = new Size(lblW, h) };
        txtDns = new TextBox { Location = new Point(120, y), Size = new Size(txtW, h) };
        y += 45;

        btnApply = new Button { Text = "Apply Settings", Location = new Point(120, y), Size = new Size(120, 40) };
        btnApply.Click += btnApply_Click;

        // Copy button next to Apply button
        btnCopy = new Button { Text = "Copy Config", Location = new Point(250, y), Size = new Size(100, 40) };
        btnCopy.Click += btnCopy_Click;
        y += 50;

        lblStatus = new Label { Text = "Ready", Location = new Point(20, y), Size = new Size(340, h), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };

        // Status strip with service status and version
        statusStrip = new StatusStrip();
        lblServiceStatus = new ToolStripStatusLabel { Text = "Checking Service..." };
        lblVersion = new ToolStripStatusLabel { Alignment = ToolStripItemAlignment.Right };
        statusStrip.Items.Add(lblServiceStatus);
        statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true }); // Spacer
        statusStrip.Items.Add(lblVersion);

        // Set version from assembly info
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "Unknown";
        lblVersion.Text = $"v{version}";

        // Set tooltips
        toolTip.SetToolTip(txtIp, "Enter IPv4 address (e.g., 192.168.1.100)");
        toolTip.SetToolTip(txtSubnet, "Enter subnet mask (e.g., 255.255.255.0)");
        toolTip.SetToolTip(txtGateway, "Enter gateway address (optional)");
        toolTip.SetToolTip(txtDns, "Enter DNS servers, comma-separated (e.g., 8.8.8.8, 8.8.4.4)");
        toolTip.SetToolTip(chkDhcp, "Enable to obtain IP address automatically from DHCP server");
        toolTip.SetToolTip(btnRefresh, "Refresh adapter list");
        toolTip.SetToolTip(btnCopy, "Copy current configuration to clipboard");

        this.Controls.AddRange(new Control[] { lblAdapter, cmbAdapters, btnRefresh, chkDhcp, lblIp, txtIp, lblSubnet, txtSubnet, lblGateway, txtGateway, lblDns, txtDns, btnApply, btnCopy, lblStatus, statusStrip });
    }
}
