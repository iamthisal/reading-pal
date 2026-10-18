// End-to-end tests share one running system (and some stop its containers), so they run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
