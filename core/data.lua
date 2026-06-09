-- data.lua
-- Thunderstorm scene data
-- Document Version 0.1.2

return {

	status = {
		start   = false,
		middle  = false,
		finish  = false,
		intense = false,
		rain    = false,
	},

	shuffle = {

		windStrength = {
			options = {
				light = {
					text      = "Light",
					allStatus = { intense = false },
				},
				strong = {
					text      = "Strong",
					allStatus = { intense = true },
				},
			},
		},

		weather = {
			options = {
				sun = {
					text          = "The rain stops, giving you and {people[1].firstName} a reprieve from the storm.",
					allStatus     = { rain = true },
					statusWeights = { base = 0.5, middle = 2 },
					setStatus     = { rain = false },
				},
				wind = {
					text          = "{shuffle.windStrength} winds buffet you and {people[1].firstName} around.",
					statusWeights = { start = 8 },
				},
				drizzle = {
					text          = "A light drizzle drifts down from the sky.",
					statusWeights = { base = 2, start = 0.2, finish = 8 },
					setStatus     = { rain = true },
				},
				downpour = {
					text          = "A heavy downpour drenches the landscape.",
					statusWeights = { start = 0.2, finish = 0.2, intense = 8 },
					setStatus     = { rain = true },
				},
				lightning = {
					text          = "Lightning flashes, and thunder rolls through the air.",
					repeatChance  = 0.5,
					statusWeights = { start = 8, intense = 8 },
				},
			},
		},

	},

	buttons = {

		linger = {
			text      = "Linger",
			position  = 1,
			anyStatus = { start = true, middle = true, finish = true },
			tap  = { text = "{shuffle.weather}" },
			hold = { text = "{shuffle.weather}" },
		},

		intensity = {
			text      = "Intensity\nTap: Light\nHold: Heavy",
			position  = 2,
			anyStatus = { start = true, middle = true, finish = true },
			tap = {
				setStatus = { intense = false },
				text      = "The storm calms.\n{shuffle.weather}",
			},
			hold = {
				setStatus = { intense = true },
				text      = "The storm intensifies!\n{shuffle.weather}",
			},
		},

		start = {
			text      = "Approach\nTap: Light\nHold: Heavy",
			position  = 0,
			allStatus = { start = false, middle = false, finish = false },
			setStatus = { start = true },
			tap = {
				setStatus = { intense = false },
				text      = "Clouds drift overhead, filling the sky.\n{shuffle.weather}",
			},
			hold = {
				setStatus = { intense = true },
				text      = "Dark clouds ominously approach.\n{shuffle.weather}",
			},
		},

		middle = {
			text      = "Arrive\nTap: Light\nHold: Heavy",
			position  = 0,
			allStatus = { start = true },
			setStatus = { start = false, middle = true },
			tap = {
				setStatus = { intense = false },
				text      = "The storm arrives gently.\n{shuffle.weather}",
			},
			hold = {
				setStatus = { intense = true },
				text      = "The storm arrives suddenly.\n{shuffle.weather}",
			},
		},

		finish = {
			text      = "Ending\nTap: Light\nHold: Heavy",
			position  = 0,
			allStatus = { middle = true },
			setStatus = { middle = false, finish = true },
			tap = {
				setStatus = { intense = false },
				text      = "The storm starts drifting away.\n{shuffle.weather}",
			},
			hold = {
				setStatus = { intense = true },
				text      = "The storm barrels through the end.\n{shuffle.weather}",
			},
		},

		stop = {
			text      = "Stop",
			position  = 0,
			allStatus = { finish = true },
			setStatus = { finish = false },
			tap  = { text = "The storm ends." },
			hold = { text = "The storm ends." },
		},

	},

}
