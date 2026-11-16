# Changelog

## [2.2.4](https://github.com/MapColonies/GpkgMerger/compare/v2.2.3...v2.2.4) (2026-11-16)


### Bug Fixes

* config traversal, timer rethrow, stack-trace reset, lock scope (MAPCO-11323) ([#258](https://github.com/MapColonies/GpkgMerger/issues/258)) ([8bfc39d](https://github.com/MapColonies/GpkgMerger/commit/8bfc39db81e0d5c88ecf7631be73cac5912faf99))


### Performance Improvements

* **fs:** probe tile extensions with File.Exists instead of directory glob (MAPCO-11364 P14) ([#261](https://github.com/MapColonies/GpkgMerger/issues/261)) ([a665c22](https://github.com/MapColonies/GpkgMerger/commit/a665c22b4a4bb8b58bd422ca19f58b7724a22b8f))
* **gpkg:** connection reuse + prepared bulk-insert + keyset paging (MAPCO-11320) ([#255](https://github.com/MapColonies/GpkgMerger/issues/255)) ([023d0b3](https://github.com/MapColonies/GpkgMerger/commit/023d0b3a7f63825d7f45a02549e713f2c08e412c))
* **image:** remove per-pixel allocs + redundant merged-image copy (MAPCO-11321) ([#256](https://github.com/MapColonies/GpkgMerger/issues/256)) ([ececf03](https://github.com/MapColonies/GpkgMerger/commit/ececf03077b85ae415306be75315163015cb039f))
* **s3:** discover zoom levels with one delimited LIST instead of 25 probes (MAPCO-11364 P11) ([#262](https://github.com/MapColonies/GpkgMerger/issues/262)) ([ffb3062](https://github.com/MapColonies/GpkgMerger/commit/ffb3062c1315374b773177ae02c8f07ce5db3810))
* **s3:** HEAD-based existence + parallel tile upload (MAPCO-11322) ([#257](https://github.com/MapColonies/GpkgMerger/issues/257)) ([7d6764e](https://github.com/MapColonies/GpkgMerger/commit/7d6764e59292ce6f367b4e0e68c110de1e92de94))
* **service:** parallelize intra-task tile merge (MAPCO-11325) ([#260](https://github.com/MapColonies/GpkgMerger/issues/260)) ([0731785](https://github.com/MapColonies/GpkgMerger/commit/0731785ca1639c68c008b958f93c36a1af06317b))

## [2.2.3](https://github.com/MapColonies/GpkgMerger/compare/v2.2.2...v2.2.3) (2026-05-19)


### Bug Fixes

* correct tile size calculation and adjust loop boundaries for inc… ([#253](https://github.com/MapColonies/GpkgMerger/issues/253)) ([59a8a0f](https://github.com/MapColonies/GpkgMerger/commit/59a8a0f857fe10b2082241f0ecf4d13dafb1b27e))

## [2.2.2](https://github.com/MapColonies/GpkgMerger/compare/v2.2.1...v2.2.2) (2026-01-29)


### Bug Fixes

* add GitHub token to artifact update steps in CI workflow ([#196](https://github.com/MapColonies/GpkgMerger/issues/196)) ([1174f6d](https://github.com/MapColonies/GpkgMerger/commit/1174f6d6d158a9e845624190743f37cb6a319435))
* update Docker build context for service and CLI images ([c448e23](https://github.com/MapColonies/GpkgMerger/commit/c448e234829a8badb6e2a54504ba094e35951df2))

## [2.2.1](https://github.com/MapColonies/GpkgMerger/compare/v2.2.0...v2.2.1) (2026-01-29)


### Bug Fixes

* update build and push workflow for improved artifact management ([#193](https://github.com/MapColonies/GpkgMerger/issues/193)) ([f2d37b1](https://github.com/MapColonies/GpkgMerger/commit/f2d37b10ced39e68be81175fb269be98337eca83))

## [2.2.0](https://github.com/MapColonies/GpkgMerger/compare/2.1.7...v2.2.0) (2026-01-29)


### Features

* update CI workflows and add release management configuration ([#185](https://github.com/MapColonies/GpkgMerger/issues/185)) ([fb7c8bf](https://github.com/MapColonies/GpkgMerger/commit/fb7c8bfe81a0c574b963829a2766325d40c81f75))
