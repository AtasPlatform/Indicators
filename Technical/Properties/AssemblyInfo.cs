using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Technical Indicators")]
[assembly: AssemblyDescription("")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("38926823-8d53-4ed1-8d11-a88a82ceba34")]

// The heatmap VWAP engine and its types are internal; Tests/Indicators.Tests (OFT.Indicators.Tests, signed with
// OFT.snk) runs it against the chart VWAP.
[assembly: InternalsVisibleTo("OFT.Indicators.Tests, PublicKey=00240000048000009400000006020000002400005253413100040000010001005100a397b02d18dcee1a963bc91ea647895ae12b25f1d549eb1856066b3dfdadacd5b79ab770858fa49bd3119caf9228da5473f15198b111b10556997b78e43ea5e6db4896eddb141ea55ed3e2934e179543fc5bb12e72b0d15a167648c754ce9d1aeeec47d467425bac2e295b31fa86aedfd38fc800fa30e08ef7d881e42fa4")]
