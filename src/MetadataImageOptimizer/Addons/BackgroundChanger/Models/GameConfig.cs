using System.Collections.Generic;

namespace MetadataImageOptimizer.Addons.BackgroundChanger.Model
{
    public class GameConfig
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DateLastRefresh { get; set; }
        public List<GameImage> Items { get; set; }
    }
}
