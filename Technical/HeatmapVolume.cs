namespace ATAS.Indicators.Technical
{
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	public class HeatmapVolume : Indicator
    {
	    private PaintbarsDataSeries _paint = new("paint");
	    private ValueDataSeries _close = new("close")
	    {
			VisualType = VisualMode.Histogram
	    };

	    private RangeDataSeries _range = new("range")
	    {
			RangeColor = Colors.LightSeaGreen
	    };

	    private SMA _sma = new();
	    private StdDev _stdDev = new();
	    private decimal _thresholdExtraHigh;
	    private decimal _thresholdHigh;
	    private decimal _thresholdMedium;
	    private decimal _thresholdNormal;
	    private bool _showAsOscillator;

	    [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "SMA", Order = 100)]
	    public int SmaPeriod
	    {
		    get => _sma.Period;
		    set
		    {
			    _sma.Period = value;
				RecalculateValues();
		    }
	    }

	    [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "StdDev", Order = 110)]
	    public int StdPeriod
	    {
		    get => _stdDev.Period;
		    set
		    {
			    _stdDev.Period = value;
				RecalculateValues();
		    }
	    }

		[Display(ResourceType = typeof(Resources), Name = "ExtraHighVolumeThreshold", GroupName = "Settings", Order = 200)]
	    public decimal ThresholdExtraHigh
		{
		    get => _thresholdExtraHigh;
		    set
		    {
			    _thresholdExtraHigh = value;
				RecalculateValues();
		    }
	    }
		
		[Display(ResourceType = typeof(Resources), Name = "HighVolumeThreshold", GroupName = "Settings", Order = 210)]
	    public decimal ThresholdHigh
		{
		    get => _thresholdHigh;
		    set
		    {
				_thresholdHigh = value;
				RecalculateValues();
		    }
	    }
		
		[Display(ResourceType = typeof(Resources), Name = "MediumVolumeThreshold", GroupName = "Settings", Order = 220)]
	    public decimal ThresholdMedium
		{
		    get => _thresholdMedium;
		    set
		    {
				_thresholdMedium = value;
				RecalculateValues();
		    }
	    }
		
		[Display(ResourceType = typeof(Resources), Name = "NormalVolumeThreshold", GroupName = "Settings", Order = 230)]
	    public decimal ThresholdNormal
		{
		    get => _thresholdNormal;
		    set
		    {
				_thresholdNormal = value;
				RecalculateValues();
		    }
	    }


		[Display(ResourceType = typeof(Resources), Name = "ShowAsOscillator", GroupName = "Settings", Order = 240)]
	    public bool ShowAsOscillator
		{
		    get => _showAsOscillator;
		    set
		    {
			    _showAsOscillator = value;
				RecalculateValues();
		    }
	    }
		
		public HeatmapVolume()
		    : base(true)
	    {
		    Panel = IndicatorDataProvider.NewPanel;
		    DenyToChangePanel = true;
		    EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);
			
			DataSeries[0] = _range;

			DataSeries.Add(_close);
			DataSeries.Add(_paint);
	    }

	    protected override void OnCalculate(int bar, decimal value)
	    {
		    if (bar == 0)
		    {
				DataSeries.ForEach(x=>x.Clear());
		    }


		    var candle = GetCandle(bar);
		    var mean = _sma.Calculate(bar, candle.Volume);
		    var std = _stdDev.Calculate(bar, candle.Volume);
		    var stdBar = (candle.Volume - mean) / std;

		    var dir = candle.Close > candle.Open;
		    var v = ShowAsOscillator ? candle.Volume - mean : candle.Volume;
		    var mosc = ShowAsOscillator ? 0 : mean;

	    }
    }
}
