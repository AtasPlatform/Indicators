namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Q Stick")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45298-q-stick")]
	public class QStick : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _openCloseSeries = new("OpenClose");

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization) { UseMinimizedModeIfEnabled = true };
		private int _period = 10;

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

		public QStick()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			LineSeries.Add(new LineSeries(Resources.ZeroValue)
			{
				Color = Colors.Gray, 
				Value = 0, 
				Width = 2
			});
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_openCloseSeries[bar] = candle.Close - candle.Open;

			if (bar < _period)
				return;

			_renderSeries[bar] = _openCloseSeries.CalcSum(_period, bar) / _period;
		}

		#endregion
	}
}