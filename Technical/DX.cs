namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

	[DisplayName("DX")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.DXDescription))]
    [HelpLink("https://help.atas.net/support/solutions/articles/72000606735-dx-indicator")]
	public class DX : Indicator
	{
		#region Fields

		private readonly DINeg _diNeg = new() { Period = 10 };
		private readonly DIPos _diPos = new() { Period = 10 };

		private readonly ValueDataSeries _posSeries;
		private readonly ValueDataSeries _negSeries;

        #endregion

        #region Properties

        [Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Period),
			GroupName = nameof(Strings.Common),
            Description = nameof(Strings.PeriodDescription),
            Order = 20)]
		[Range(1, 10000)]
		public int Period
		{
			get => _diPos.Period;
			set
			{
				_diPos.Period = _diNeg.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DX()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_posSeries = CreateDisplaySeries((ValueDataSeries)_diPos.DataSeries[0]);
			_negSeries = CreateDisplaySeries((ValueDataSeries)_diNeg.DataSeries[0]);
			DataSeries.Add(_posSeries);
			DataSeries.Add(_negSeries);
			DataSeries[0].IgnoredByAlerts = DataSeries[1].IgnoredByAlerts = true;

			Add(_diNeg);
			Add(_diPos);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var pos = _diPos[bar];
			var neg = _diNeg[bar];

			_posSeries[bar] = pos;
			_negSeries[bar] = neg;

			var sum = pos + neg;
			var diff = Math.Abs(pos - neg);

			this[bar] = sum != 0m ? 100 * diff / sum : 0m;
		}

		#endregion

		#region Private static methods

		private static ValueDataSeries CreateDisplaySeries(ValueDataSeries source)
		{
			// Keep the serialized identity, but do not clear a nested indicator's output with DX.
			return new ValueDataSeries(source.Id, source.Name)
			{
				Color = source.Color,
				LineDashStyle = source.LineDashStyle,
				VisualType = source.VisualType
			};
		}

		#endregion
	}
}
