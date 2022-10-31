namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Zero Lag Exponential Moving Average")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45287-zero-lag-exponential-moving-average")]
	public class ZLEMA : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };
		private int _length = 4;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				_length = (int)Math.Ceiling((value - 1) / 2m);
				RecalculateValues();
			}
		}

		#endregion
		
		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var startBar = Math.Max(0, bar - _length);

			var deLagged = 2 * value - (decimal)SourceDataSeries[startBar];

			this[bar] = _ema.Calculate(bar, deLagged);
		}

		#endregion
	}
}