namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;
    using Color = System.Drawing.Color;

    [DisplayName("Weis Wave")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.WeissWaveDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602507")]
	public class WeissWave : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
		{
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true,
			ResetAlertsOnNewBar = true
		};

		private int _filter;
		private Color _filterColor = Color.LightBlue;
		private Color _negColor = DefaultColors.Red;
		private Color _posColor = DefaultColors.Green;

		#endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Up), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.BullishColorDescription), Order = 610)]
        public CrossColor PosColor
        {
	        get => _posColor.Convert();
	        set
	        {
		        _posColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Down), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.BearishColorDescription), Order = 620)]
        public CrossColor NegColor
        {
	        get => _negColor.Convert();
	        set
	        {
		        _negColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.FilterColorDescription), Order = 630)]
        public CrossColor FilterColor
        {
	        get => _filterColor.Convert();
	        set
	        {
		        _filterColor = value.Convert();
		        RecalculateValues();
	        }
        }

		[Range(0, int.MaxValue)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MaximumFilterDescription))]
        public int Filter
		{
			get => _filter;
			set
			{
				_filter = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public WeissWave()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
            if (bar == 0)
			{
				_renderSeries[bar] = candle.Volume;
				_renderSeries.Colors[bar] = candle.Open < candle.Close ? _posColor : _negColor;
			}
			else
            {
	            var prevCandle = GetCandle(bar - 1);

	            var renderValue = Math.Sign(candle.Open - candle.Close) == Math.Sign(prevCandle.Open - prevCandle.Close) 
					? _renderSeries[bar - 1] + candle.Volume
					: candle.Volume;

	            _renderSeries[bar] = renderValue;
	            _renderSeries.Colors[bar] = candle.Open < candle.Close
		            ? _posColor
		            : _negColor;
            }

            if (_filter <= 0)
	            return;

            if (_renderSeries[bar] > _filter)
				_renderSeries.Colors[bar] = _filterColor;
		}

		#endregion
	}
}