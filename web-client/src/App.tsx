import { BrowserRouter, Route, Routes } from 'react-router-dom'
import Home from './pages/Home'
import RtcPage from './pages/RtcPage'

const App = () => {


	return <BrowserRouter>
		<Routes>
			<Route path="/" element={<Home />} />
			<Route path="/rtc" element={<RtcPage />} />
		</Routes>
	</BrowserRouter>
}

export default App
