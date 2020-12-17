namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Block Moving Average")]
	public class BlockMA : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);
		private readonly ValueDataSeries _top1 = new ValueDataSeries("top1");
		private readonly ValueDataSeries _top2 = new ValueDataSeries("top2");
		private readonly ValueDataSeries _mid1 = new ValueDataSeries("mid1");
		private readonly ValueDataSeries _mid2 = new ValueDataSeries("mid2");
		private readonly ValueDataSeries _bot1 = new ValueDataSeries("bot1");
		private readonly ValueDataSeries _bot2 = new ValueDataSeries("bot2");
		private ATR _atr = new ATR();
		private decimal _multiplier2;
		private decimal _multiplier1;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ATR", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _atr.Period;
			set
			{
				if (value <= 0)
					return;

				_atr.Period = value;
				RecalculateValues();
			}
		}
		[Display(ResourceType = typeof(Resources), Name = "Multiplier1", GroupName = "Settings", Order = 110)]
		public decimal Multiplier1
		{
			get => _multiplier1;
			set
			{
				if (value <= 0)
					return;

				_multiplier1 = value;
				RecalculateValues();
			}
		}
		[Display(ResourceType = typeof(Resources), Name = "Multiplier2", GroupName = "Settings", Order = 120)]
		public decimal Multiplier2
		{
			get => _multiplier2;
			set
			{
				if (value <= 0)
					return;

				_multiplier2 = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BlockMA():base(true)
		{
			_atr.Period = 10;
			_multiplier1 = 1;
			_multiplier2 = 2;
			Add(_atr);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var box1 = _multiplier1 * _atr[bar] / 2;
			var box2 = _multiplier2 * _atr[bar] / 2;


		}

		#endregion
	}
}