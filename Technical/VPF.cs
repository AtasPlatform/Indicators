namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Voss Predictive Filter")]
	public class VPF : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _flit = new ValueDataSeries("Flit");
		private readonly ValueDataSeries _voss = new ValueDataSeries("Voss");

		private decimal _bandWidth;
		private int _order;

		private int _period;
		private int _predict;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 0)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Predict", GroupName = "Common", Order = 1)]
		public int Predict
		{
			get => _predict;
			set
			{
				if (value <= 0)
					return;

				_predict = value;
				_order = _predict * 3;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BBandsWidth", GroupName = "Common", Order = 2)]
		public decimal BandsWidth
		{
			get => _bandWidth;
			set
			{
				if (value <= 0)
					return;

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

			_bandWidth = 0.25m;
			_period = 20;
			_predict = 3;

			_voss.ShowZeroValue = false;
			_voss.Color = Colors.DodgerBlue;
			_voss.Width = 2;

			_flit.ShowZeroValue = false;
			_flit.Color = Colors.Red;
			_flit.Width = 2;

			DataSeries[0] = _voss;
			DataSeries.Add(_flit);
			LineSeries.Add(new LineSeries("ZeroLine") { Value = 0, IsHidden = true });
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

			if (bar >= _order)
			{
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

				var flitValue = Math.Round(0.5 * s3 * Convert.ToDouble(x1) + f1 * s2 * Convert.ToDouble(_flit[bar - 1]) - s1 * Convert.ToDouble(_flit[bar - 2]),
					5);
				_flit[bar] = Convert.ToDecimal(flitValue);

				var vossValue = Math.Round(x2 * flitValue - sumC, 5);
				_voss[bar] = Convert.ToDecimal(vossValue);
			}
		}

		#endregion
	}
}