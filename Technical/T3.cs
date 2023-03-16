namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("T3")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45336-t3")]
	public class T3 : Indicator
	{
		#region Fields

		private readonly List<EMA> _emaSix = new();
		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
		{
			UseMinimizedModeIfEnabled = true
		};
		private decimal _multiplier = 1;
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
				_emaSix.ForEach(x => x.Period = value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "Settings", Order = 110)]
        [Range(1,1000000)]
		public decimal Multiplier
		{
			get => _multiplier;
			set
			{
				_multiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public T3()
		{
			for (var i = 0; i < 6; i++)
			{
				_emaSix.Add(new EMA
					{ Period = _period });
			}

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_emaSix[0].Calculate(bar, value);

			for (var i = 1; i < _emaSix.Count; i++)
				_emaSix[i].Calculate(bar, _emaSix[i - 1][bar]);

			_renderSeries[bar] = -(decimal)Math.Pow((double)_multiplier, 3) * _emaSix[5][bar] +
				3 * _multiplier * _multiplier * (1 + _multiplier) * _emaSix[4][bar] -
				3 * _multiplier * (1 + _multiplier) * (1 + _multiplier) * _emaSix[3][bar] +
				(decimal)Math.Pow(1 + (double)_multiplier, 3) * _emaSix[2][bar];
		}

		#endregion
	}
}