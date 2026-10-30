import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { api, type DeviantArtStats, type DeviationStats, type InspirationStats, type PipelineStats, type PresetStats, type SourceKind } from '../api'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'
import { useLoad } from '../live/useLoad'
import { Pager } from '../paging/Pager'
import { usePagedItems } from '../paging/usePagedItems'
import { DailyBarChart } from './DailyBarChart'
import { formatAge, formatCompact, formatCount, formatDay, formatDecimal, formatPercent, formatSigned } from './format'
import { nextSortOrder, sortRows, type SortOrder } from './sortRows'

export function StatsPage() {
  const stats = useLoad(api.stats)
  const [refreshing, setRefreshing] = useState(false)
  const [refreshError, setRefreshError] = useState<string>()
  const autoRefreshed = useRef(false)
  const { reload } = stats

  const refresh = useCallback(
    async (force: boolean) => {
      setRefreshing(true)
      try {
        setRefreshError((await api.refreshDeviantArtStats(force)).error ?? undefined)
        reload()
      } catch (failure) {
        setRefreshError((failure as Error).message)
      } finally {
        setRefreshing(false)
      }
    },
    [reload],
  )

  const stale = stats.data?.deviantArt.stale ?? false
  useEffect(() => {
    if (!stale || autoRefreshed.current) return
    autoRefreshed.current = true
    void refresh(false)
  }, [stale, refresh])

  const deviantArt = stats.data?.deviantArt

  return (
    <div>
      <div className="toolbar">
        <h2>Stats</h2>
        {deviantArt?.connected && (
          <>
            <span className="hint">
              {refreshing
                ? 'Fetching DeviantArt numbers…'
                : deviantArt.fetchedAt
                  ? `DeviantArt numbers from ${new Date(deviantArt.fetchedAt).toLocaleString()}`
                  : 'DeviantArt numbers not fetched yet'}
            </span>
            <button type="button" disabled={refreshing} onClick={() => void refresh(true)}>
              Refresh
            </button>
          </>
        )}
      </div>
      {stats.error && <p className="error">{stats.error}</p>}
      {refreshError && <p className="error">{refreshError} The numbers below are the last ones fetched.</p>}
      {stats.data && (
        <div className="stats-sections">
          <DeviantArtSection stats={stats.data.deviantArt} />
          <PresetSection presets={stats.data.presets} />
          <PipelineSection pipeline={stats.data.pipeline} />
          <InspirationSection inspiration={stats.data.inspiration} />
        </div>
      )}
    </div>
  )
}

function StatTile({ label, value, detail, to }: { label: string; value: string; detail?: string; to?: string }) {
  const content = (
    <>
      <span className="stat-label">{label}</span>
      <span className="stat-value">{value}</span>
      {detail && <span className="stat-detail">{detail}</span>}
    </>
  )
  return to ? (
    <Link to={to} className="stat-tile">
      {content}
    </Link>
  ) : (
    <div className="stat-tile">{content}</div>
  )
}

function DeviantArtSection({ stats }: { stats: DeviantArtStats }) {
  const { totals } = stats
  return (
    <section className="panel stats-section">
      <h3>DeviantArt</h3>
      {!stats.connected && (
        <p className="hint">
          Connect DeviantArt in <Link to="/settings">Settings</Link> to see views and favourites.
        </p>
      )}
      {totals.published === 0 ? (
        <p className="hint">Nothing published yet.</p>
      ) : (
        <>
          <div className="stat-tiles">
            <StatTile label="Published" value={totals.published.toLocaleString()} to="/published" />
            <StatTile label="Views" value={formatCompact(totals.views)} detail={`${formatSigned(totals.viewsLast7Days)} in 7 days`} />
            <StatTile
              label="Favourites"
              value={formatCompact(totals.favourites)}
              detail={`${formatSigned(totals.favouritesLast7Days)} in 7 days`}
            />
            <StatTile label="Comments" value={formatCompact(totals.comments)} />
            <StatTile label="Downloads" value={formatCompact(totals.downloads)} />
          </div>
          {totals.tracked < totals.published && (
            <p className="hint">
              {totals.published - totals.tracked} of {totals.published} published sets have no numbers yet.
            </p>
          )}
          {stats.historySince && (
            <>
              <div className="stats-charts">
                <DailyBarChart
                  title="Views gained per day"
                  unit="views"
                  values={stats.daily.map((day) => ({ day: day.day, value: day.views }))}
                />
                <DailyBarChart
                  title="Favourites gained per day"
                  unit="favourites"
                  values={stats.daily.map((day) => ({ day: day.day, value: day.favourites }))}
                />
              </div>
              <p className="hint">
                Daily numbers are kept since {formatDay(stats.historySince, true)}, and only for days the Stats tab was
                opened. A gap shows up as one larger day after it.
              </p>
            </>
          )}
          <DeviationTable deviations={stats.deviations} />
        </>
      )}
    </section>
  )
}

const TABLE_PAGE_SIZE = 25

interface Column<T> {
  key: keyof T
  label: string
  render: (row: T) => ReactNode
  numeric?: boolean
}

interface TableProps<T> {
  rows: T[]
  columns: Column<T>[]
  initial: SortOrder<T>
  rowKey: (row: T) => string | number
}

function SortableTable<T>({ rows, columns, initial, rowKey }: TableProps<T>) {
  const [order, setOrder] = useState(initial)
  return <TableView rows={sortRows(rows, order)} columns={columns} order={order} onSort={setOrder} rowKey={rowKey} />
}

/** Sorted over all rows first, so page 1 is the real top; a new sort goes back to page 1. */
function PagedSortableTable<T>({ pageParam, ...props }: TableProps<T> & { pageParam: string }) {
  const [order, setOrder] = useState(props.initial)
  const sorted = useMemo(() => sortRows(props.rows, order), [props.rows, order])
  const paged = usePagedItems(sorted, pageParam, TABLE_PAGE_SIZE)
  const sort = (next: SortOrder<T>) => {
    setOrder(next)
    paged.setPage(1)
  }
  return (
    <>
      <TableView rows={paged.pageItems} columns={props.columns} order={order} onSort={sort} rowKey={props.rowKey} />
      <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />
    </>
  )
}

function TableView<T>({ rows, columns, order, onSort, rowKey }: Omit<TableProps<T>, 'initial'> & { order: SortOrder<T>; onSort: (order: SortOrder<T>) => void }) {
  return (
    <div className="stats-table-scroll">
      <table className="stats-table">
        <thead>
          <tr>
            {columns.map((column) => (
              <th
                key={String(column.key)}
                className={column.numeric ? 'number' : undefined}
                aria-sort={order.key === column.key ? (order.direction === 'asc' ? 'ascending' : 'descending') : undefined}
              >
                <button type="button" className="sort-button" onClick={() => onSort(nextSortOrder(order, column.key))}>
                  {column.label}
                  {order.key === column.key && <span aria-hidden="true">{order.direction === 'asc' ? ' ▲' : ' ▼'}</span>}
                </button>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowKey(row)}>
              {columns.map((column) => (
                <td key={String(column.key)} className={column.numeric ? 'number' : undefined}>
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

const DEVIATION_COLUMNS: Column<DeviationStats>[] = [
  {
    key: 'title',
    label: 'Deviation',
    render: (deviation) => (
      <span className="deviation-cell">
        {deviation.previewImageUrl ? <img src={deviation.previewImageUrl} alt="" loading="lazy" /> : <span className="thumb" />}
        <Link to={`/sets/${deviation.setId}`}>{deviation.title ?? `Set #${deviation.setId}`}</Link>
        <a href={deviation.url} target="_blank" rel="noreferrer" title="Open on DeviantArt" aria-label="Open on DeviantArt">
          ↗
        </a>
      </span>
    ),
  },
  { key: 'publishedAt', label: 'Published', render: (d) => new Date(d.publishedAt).toLocaleDateString() },
  { key: 'daysOnline', label: 'Online', render: (d) => formatAge(d.daysOnline), numeric: true },
  { key: 'preset', label: 'Preset', render: (d) => d.preset ?? '–' },
  { key: 'views', label: 'Views', render: (d) => formatCount(d.views), numeric: true },
  { key: 'viewsLast7Days', label: 'Views 7d', render: (d) => formatSigned(d.viewsLast7Days), numeric: true },
  { key: 'viewsPerDay', label: 'Views/day', render: (d) => formatDecimal(d.viewsPerDay), numeric: true },
  { key: 'favourites', label: 'Favs', render: (d) => formatCount(d.favourites), numeric: true },
  { key: 'favouritesLast7Days', label: 'Favs 7d', render: (d) => formatSigned(d.favouritesLast7Days), numeric: true },
  { key: 'favouritesPer100Views', label: 'Favs/100 views', render: (d) => formatDecimal(d.favouritesPer100Views), numeric: true },
  { key: 'comments', label: 'Comments', render: (d) => formatCount(d.comments), numeric: true },
  { key: 'downloads', label: 'Downloads', render: (d) => formatCount(d.downloads), numeric: true },
]

function DeviationTable({ deviations }: { deviations: DeviationStats[] }) {
  return (
    <PagedSortableTable
      rows={deviations}
      pageParam="deviationsPage"
      columns={DEVIATION_COLUMNS}
      initial={{ key: 'views', direction: 'desc' }}
      rowKey={(deviation) => deviation.setId}
    />
  )
}

const PRESET_COLUMNS: Column<PresetStats>[] = [
  { key: 'name', label: 'Preset', render: (p) => p.name },
  { key: 'images', label: 'Images', render: (p) => formatCount(p.images), numeric: true },
  { key: 'picks', label: 'Picks', render: (p) => formatCount(p.picks), numeric: true },
  { key: 'pickRate', label: 'Pick rate', render: (p) => formatPercent(p.pickRate), numeric: true },
  { key: 'published', label: 'Published', render: (p) => formatCount(p.published), numeric: true },
  { key: 'averageViews', label: 'Avg views', render: (p) => formatDecimal(p.averageViews), numeric: true },
  { key: 'averageFavourites', label: 'Avg favs', render: (p) => formatDecimal(p.averageFavourites), numeric: true },
]

function PresetSection({ presets }: { presets: PresetStats[] }) {
  return (
    <section className="panel stats-section">
      <h3>Presets</h3>
      {presets.length === 0 ? (
        <p className="hint">No images generated yet.</p>
      ) : (
        <>
          <SortableTable
            rows={presets}
            columns={PRESET_COLUMNS}
            initial={{ key: 'images', direction: 'desc' }}
            rowKey={(preset) => preset.presetId ?? 'none'}
          />
          <p className="hint">
            An image belongs to the preset whose art style is in its prompt. A deviation counts for the preset of its
            pick #1.
          </p>
        </>
      )}
    </section>
  )
}

function PipelineSection({ pipeline }: { pipeline: PipelineStats }) {
  const { stages } = pipeline
  return (
    <section className="panel stats-section">
      <h3>Studio</h3>
      <div className="stat-tiles">
        <StatTile label="Drafts" value={stages.drafts.toLocaleString()} to="/drafts" />
        <StatTile label="Sets" value={stages.working.toLocaleString()} to="/sets" />
        <StatTile label="Post" value={stages.post.toLocaleString()} to="/post" />
        <StatTile label="Stashed" value={stages.stashed.toLocaleString()} to="/stashed" />
        <StatTile label="Published" value={stages.published.toLocaleString()} to="/published" />
        <StatTile label="Archived" value={stages.archived.toLocaleString()} to="/archive" />
      </div>
      <div className="stat-tiles">
        <StatTile
          label="Images"
          value={formatCompact(pipeline.images)}
          detail={`${pipeline.imagesLast7Days.toLocaleString()} in 7 days, ${pipeline.imagesLast30Days.toLocaleString()} in 30`}
        />
        <StatTile label="Pick rate" value={formatPercent(pipeline.pickRate)} detail="of images in sets that reached Post" />
        <StatTile
          label="Render time"
          value={pipeline.secondsPerImage === null ? '–' : `${formatDecimal(pipeline.secondsPerImage)} s`}
          detail="per image, last 30 days"
        />
        <StatTile
          label="Jobs, last 30 days"
          value={pipeline.jobsCompletedLast30Days.toLocaleString()}
          detail={`${pipeline.jobsFailedLast30Days.toLocaleString()} failed`}
        />
        <StatTile
          label="To Post"
          value={pipeline.medianDaysToReady === null ? '–' : `${formatDecimal(pipeline.medianDaysToReady)} d`}
          detail="median from created"
        />
        <StatTile
          label="To published"
          value={pipeline.medianDaysReadyToPublished === null ? '–' : `${formatDecimal(pipeline.medianDaysReadyToPublished)} d`}
          detail="median from Post"
        />
      </div>
      <div className="stats-charts">
        <DailyBarChart
          title="Images per day"
          unit="images"
          values={pipeline.imagesPerDay.map((day) => ({ day: day.day, value: day.count }))}
        />
      </div>
    </section>
  )
}

const SOURCE_LABELS: Record<SourceKind, string> = {
  DeviantArt: 'DeviantArt',
  Upload: 'Uploaded image',
  Paste: 'Pasted image',
  None: 'No source',
}

function InspirationSection({ inspiration }: { inspiration: InspirationStats }) {
  return (
    <section className="panel stats-section">
      <h3>Inspiration</h3>
      <div className="stat-tiles">
        {inspiration.sources.map((source) => (
          <StatTile key={source.source} label={SOURCE_LABELS[source.source]} value={source.sets.toLocaleString()} detail="sets" />
        ))}
      </div>
      {inspiration.topArtists.length > 0 && (
        <div className="stats-table-scroll">
          <table className="stats-table">
            <thead>
              <tr>
                <th>Artist</th>
                <th className="number">Sets</th>
                <th className="number">Published</th>
                <th className="number">Avg favs</th>
              </tr>
            </thead>
            <tbody>
              {inspiration.topArtists.map((artist) => (
                <tr key={artist.username}>
                  <td>
                    <DeviantArtUserLink username={artist.username} />
                  </td>
                  <td className="number">{artist.sets.toLocaleString()}</td>
                  <td className="number">{artist.published.toLocaleString()}</td>
                  <td className="number">{formatDecimal(artist.averageFavourites)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
