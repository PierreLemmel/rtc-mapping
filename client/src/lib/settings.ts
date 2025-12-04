import settings from '../../settings.json'

export type Settings = typeof settings

export const getSettings = (): Settings => {
    return settings
}