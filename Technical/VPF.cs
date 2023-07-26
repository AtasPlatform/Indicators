namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

using Utils.Common.Logging;

[DisplayName("Voss Predictive Filter")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38171-voss-predictive-filter")]
public class VPF : Indicator
{
	#region Fields

	private readonly ValueDataSeries _flit = new("FlitId", "Flit")
	{
		Color = Colors.DodgerBlue,
		Width = 2,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _voss = new("VossId", "Voss")
	{
		Color = Colors.Red,
		Width = 2,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private decimal _bandWidth = 0.25m;
	private int _order;

	private int _period = 20;
	private int _predict = 3;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings")]
	[Range(1, 100000)]
	public int Period
	{
		get => _period;
		set
		{
			_period = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Predict", GroupName = "Settings")]
	[Range(1, 1000000)]
	public int Predict
	{
		get => _predict;
		set
		{
			_predict = value;
			_order = _predict * 3;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "BBandsWidth", GroupName = "Settings")]
	[Range(0, 4)]
	public decimal BandsWidth
	{
		get => _bandWidth;
		set
		{
			_bandWidth = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public VPF()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		DenyToChangePanel = true;

		DataSeries[0] = _voss;
		DataSeries.Add(_flit);
		LineSeries.Add(new LineSeries("ZeroLineId", "ZeroLine")
		{
			Value = 0,
			Color = DefaultColors.Silver.Convert()
		});
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_voss.Clear();
			_flit.Clear();
		}

		if (bar < _order)
			return;

		var f1 = Math.Cos(2.0 * Math.PI / Period);
		var g1 = Math.Cos(Convert.ToDouble(BandsWidth) * 2.0 * Math.PI / Period);

		var s1 = 1.0 / g1 - Math.Sqrt(1.0 / (g1 * g1) - 1.0);
		var s2 = 1.0 + s1;
		var s3 = 1.0 - s1;

		var x1 = GetCandle(bar).Close - GetCandle(bar - 2).Close;
		var x2 = (3.0 + _order) / 2.0;

		var sumC = 0.0;

		for (var i = 0; i < _order; i++)
			sumC += (i + 1.0) / _order * Convert.ToDouble(_voss[bar - _order + i]);

		try
		{
			var flitValue = Math.Round(
				0.5 * s3 * (double)x1 + f1 * s2 * (double)_flit[bar - 1] - s1 * (double)_flit[bar - 2],
				5);
			_flit[bar] = (decimal)flitValue;

			var vossValue = x2 * flitValue - sumC;
			_voss[bar] = (decimal)vossValue;
		}
		catch (Exception e)
		{
			this.LogError($"{e.Message}", e);
		}
	}

	#endregion
}