namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Directional Movement Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45188-directional-movement-index")]
	public class DmIndex : Indicator
	{
		#region Fields

		private readonly ATR _atr = new();

		private readonly ValueDataSeries _dmDown = new("DmUp");
		private readonly ValueDataSeries _dmUp = new("DmDown");

		private readonly ValueDataSeries _downSeries = new(Resources.Down) { Color = DefaultColors.Red.Convert() };
		private readonly ValueDataSeries _upSeries = new(Resources.Up) { Color = DefaultColors.Blue.Convert() };

		private int _period = 14;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DmIndex()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			Add(_atr);
			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			_dmUp[bar] = candle.High - prevCandle.High;
			_dmDown[bar] = prevCandle.Low - candle.Low;

			if (bar < _period)
				return;

			var dmUpSum = _dmUp.CalcSum(_period, bar);
			var dmDownSum = _dmDown.CalcSum(_period, bar);

			var smoothedUp = dmUpSum - dmUpSum / _period + _dmUp[bar];
			var smoothedDown = dmDownSum - dmDownSum / _period + _dmDown[bar];

			_upSeries[bar] = smoothedUp / _atr[bar] * 100m;
			_downSeries[bar] = smoothedDown / _atr[bar] * 100m;
		}

		#endregion
	}
}