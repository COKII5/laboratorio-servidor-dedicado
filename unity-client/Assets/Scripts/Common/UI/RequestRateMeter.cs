namespace Lab.Common
{
    public class RequestRateMeter
    {
        const double WindowSeconds = 1.0;

        int windowStartGets;
        int windowStartPosts;
        double windowStart;

        public double GetsPerSecond { get; private set; }
        public double PostsPerSecond { get; private set; }

        public void Sample(int getCount, int postCount)
        {
            double now = RealtimeClock.Now;
            double elapsed = now - windowStart;
            if (elapsed < WindowSeconds) return;

            GetsPerSecond = (getCount - windowStartGets) / elapsed;
            PostsPerSecond = (postCount - windowStartPosts) / elapsed;
            windowStartGets = getCount;
            windowStartPosts = postCount;
            windowStart = now;
        }
    }
}
