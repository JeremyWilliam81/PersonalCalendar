import type { TimedSegment } from '../api/types'

// Positions on the time scale (research V3, contracts/ui-interaction "Week view: wide").
// Minutes are elapsed time from the day's real start instant, never wall-clock hours.

/** 48 px per hour. */
export const PX_PER_MINUTE = 0.8
/** Short events stay visible and tappable. */
export const MIN_BLOCK_PX = 24

export function minutesToPx(minutes: number): number {
  return minutes * PX_PER_MINUTE
}

/** The instant `minutes` after `dayStart`, as ISO 8601 (instant arithmetic only, no offsets). */
export function instantAfter(dayStart: string, minutes: number): string {
  return new Date(Date.parse(dayStart) + minutes * 60_000).toISOString()
}

export interface Cluster {
  segments: TimedSegment[]
  startMinutes: number
  endMinutes: number
  columnCount: number
}

/** Groups segments that overlap directly or through a chain, matching the server's columns (research V4). */
export function clustersOf(segments: TimedSegment[]): Cluster[] {
  const sorted = [...segments].sort((a, b) => a.offsetMinutes - b.offsetMinutes)
  const clusters: Cluster[] = []
  for (const segment of sorted) {
    const end = segment.offsetMinutes + segment.durationMinutes
    const current = clusters[clusters.length - 1]
    if (current && segment.offsetMinutes < current.endMinutes) {
      current.segments.push(segment)
      current.endMinutes = Math.max(current.endMinutes, end)
      current.columnCount = Math.max(current.columnCount, segment.columnCount)
    } else {
      clusters.push({ segments: [segment], startMinutes: segment.offsetMinutes, endMinutes: end, columnCount: segment.columnCount })
    }
  }
  return clusters
}
